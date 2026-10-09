import {computed, DestroyRef, inject, Injectable, signal} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {filter, tap} from 'rxjs';
import {MessageHubService, EVENTS, Message} from '../../_services/message-hub.service';

// Re-export for convenience in components
export {EVENTS};
import {TtsService} from './tts-service';
import {
  TtsAudioChunkMessage,
  TtsChunkGranularity,
  TtsStreamRequest,
  TtsStreamStatusMessage,
} from '../_models/tts-models';

/**
 * Current state of the TTS playback session.
 */
export type TtsPlaybackState = 'idle' | 'connecting' | 'playing' | 'paused' | 'error' | 'completed';

/** A decoded audio buffer paired with the metadata needed to drive the highlight signal. */
interface QueuedChunk {
  buffer: AudioBuffer;
  chunkIndex: number;
  text: string;
}

@Injectable({
  providedIn: 'root'
})
export class TtsPlaybackService {

  private readonly messageHub = inject(MessageHubService);
  private readonly ttsService = inject(TtsService);
  private readonly destroyRef = inject(DestroyRef);

  // --- Public signals ---

  /** Current playback state. */
  public readonly state = signal<TtsPlaybackState>('idle');

  /** Currently playing chapter ID (0 when idle). */
  public readonly chapterId = signal<number>(0);

  /** Total number of chunks in the current stream session. */
  public readonly totalChunks = signal<number>(0);

  /** Index of the chunk currently being played back via Web Audio (-1 until first buffer starts). */
  public readonly currentChunkIndex = signal<number>(-1);

  /** Text content of the chunk currently being played back (empty string when none). */
  public readonly currentChunkText = signal<string>('');

  /** Number of audio buffers successfully decoded and queued for playback. */
  public readonly bufferedChunks = signal<number>(0);

  /** Whether TTS is paused (user-initiated pause, not stream-level). */
  public readonly isPaused = signal<boolean>(false);

  /** Error message if the stream failed. */
  public readonly errorMessage = signal<string | null>(null);

  /** Computed playback progress as a percentage (0-100). */
  public readonly progressPercent = computed(() => {
    const total = this.totalChunks();
    if (total <= 0) return 0;
    return Math.round((this.currentChunkIndex() / total) * 100);
  });

  // --- Internal state ---

  /** Web Audio API context (lazy-initialized). */
  private audioContext: AudioContext | null = null;

  /** Queue of decoded audio buffers waiting to be played, each with its chunk metadata. */
  private readonly bufferQueue: QueuedChunk[] = [];

  /** Whether we are actively playing an audio buffer from the queue. */
  private isPlayingBuffer = false;

  /** Whether the stream session has ended (completed, cancelled, or error). */
  private streamEnded = false;

  constructor() {
    this.subscribeToSignalR();
  }

  // --- Public API ---

  /**
   * Starts a TTS streaming session for the given chapter.
   * @param chapterId EPUB chapter ID to read aloud.
   * @param startChunk Zero-based index of the first chunk to synthesize.
   * @param endChunk Zero-based index of the last chunk (inclusive). Use -1 for "all remaining".
   * @param granularity Text chunking granularity.
   * @param voiceOverride Optional voice override for this session.
   * @param speedOverride Optional speed override (0.5-4.0) for this session.
   */
  startStream(
    chapterId: number,
    startChunk = 0,
    endChunk = -1,
    granularity = TtsChunkGranularity.Sentence,
    voiceOverride?: string | null,
    speedOverride?: number | null,
  ): void {
    this.stopAndReset();

    const request: TtsStreamRequest = {
      startChunk,
      endChunk,
      granularity,
      voiceOverride,
      speedOverride,
    };

    this.chapterId.set(chapterId);
    this.state.set('connecting');
    this.errorMessage.set(null);
    this.streamEnded = false;

    this.ttsService.startStream(chapterId, request)
      .pipe(
        tap(() => {
          // Stream started on the backend. SignalR messages will begin arriving.
          if (this.state() === 'connecting') {
            this.state.set('playing');
          }
        }),
        takeUntilDestroyed(this.destroyRef),
      ).subscribe({
        error: (err) => {
          this.state.set('error');
          this.errorMessage.set(err?.message ?? 'Failed to start TTS stream');
          this.streamEnded = true;
        },
      });
  }

  /** Stops the active TTS stream and resets all state. */
  stop(): void {
    this.ttsService.stopStream().subscribe({
      error: (err) => console.error('TTS stop failed', err),
    });
    this.stopAndReset();
  }

  /** Pauses or resumes local audio playback without stopping the backend stream. */
  togglePause(): void {
    if (this.isPaused()) {
      this.resumePlayback();
    } else {
      this.pausePlayback();
    }
  }

  // --- Private methods ---

  private stopAndReset(): void {
    this.streamEnded = true;
    this.state.set('idle');
    this.chapterId.set(0);
    this.totalChunks.set(0);
    this.currentChunkIndex.set(-1);
    this.currentChunkText.set('');
    this.bufferedChunks.set(0);
    this.isPaused.set(false);
    this.errorMessage.set(null);

    // Cancel any playing audio source
    if (this.audioContext) {
      this.audioContext.close();
      this.audioContext = null;
    }
    this.bufferQueue.length = 0;
    this.isPlayingBuffer = false;
  }

  private pausePlayback(): void {
    if (!this.audioContext || this.isPaused()) return;
    this.audioContext.suspend();
    this.isPaused.set(true);
    this.state.set('paused');
  }

  private resumePlayback(): void {
    if (!this.audioContext || !this.isPaused()) return;
    this.audioContext.resume().then(() => {
      this.isPaused.set(false);
      this.state.set('playing');
    });
  }

  /** Subscribes to TTS SignalR events via the shared MessageHubService. */
  private subscribeToSignalR(): void {
    // We need to register handlers on the hub connection for TtsAudioChunk and TtsPlaybackUpdate.
    // The existing MessageHubService uses .on() in createHubConnection(). Since we can't modify that
    // method here, we use the messages$ observable which receives all events. However, the TTS events
    // are sent via SendMessageToAsync (direct to user), not through the general broadcast. They may
    // arrive as raw SignalR methods rather than wrapped in our Message<T> format.
    //
    // Approach: We subscribe to messages$ and filter for TTS event names. If the backend sends them
    // via SendMessageToAsync with method name "TtsAudioChunk" / "TtsPlaybackUpdate", they'll arrive
    // as SignalR method calls on the hub connection, not through our ReplaySubject.
    //
    // For now, we set up a pattern where the component using this service will also register
    // direct hub listeners for TTS events and forward them to us via processAudioChunk / processStatusUpdate.

    // Subscribe to general messages$ as fallback (for broadcast-style events)
    this.messageHub.messages$.pipe(
      takeUntilDestroyed(this.destroyRef),
      filter((msg: Message<any>) => {
        const eventName = msg.event;
        return eventName === EVENTS.TtsAudioChunk || eventName === EVENTS.TtsPlaybackUpdate;
      }),
    ).subscribe((msg: Message<any>) => {
      if (msg.event === EVENTS.TtsAudioChunk) {
        this.processAudioChunk(msg.payload as TtsAudioChunkMessage);
      } else if (msg.event === EVENTS.TtsPlaybackUpdate) {
        this.processStatusUpdate(msg.payload as TtsStreamStatusMessage);
      }
    });
  }

  /**
   * Processes an incoming audio chunk from SignalR. Decodes the base64 data into an AudioBuffer
   * and queues it for sequential playback.
   */
  processAudioChunk(chunk: TtsAudioChunkMessage): void {
    if (this.streamEnded) return;

    this.bufferedChunks.update(v => v + 1);

    // Decode audio data asynchronously and queue for playback.
    // The highlight signals (currentChunkIndex/currentChunkText) are set
    // only when the buffer actually starts playing, not on arrival —
    // otherwise the highlight races ahead of the audio.
    this.decodeAndQueueBase64Audio(chunk.audioBase64, chunk.chunkIndex, chunk.text ?? '');
  }

  /**
   * Processes a stream status update from SignalR. Updates progress and handles completion/error states.
   */
  processStatusUpdate(status: TtsStreamStatusMessage): void {
    if (status.isComplete) {
      this.streamEnded = true;
      this.state.set('completed');
      this.currentChunkText.set('');
    } else if (status.isCancelled ?? false) {
      this.streamEnded = true;
      this.state.set('idle');
      this.currentChunkText.set('');
    } else if (status.errorMessage) {
      this.streamEnded = true;
      this.state.set('error');
      this.errorMessage.set(status.errorMessage);
      this.currentChunkText.set('');
    }

    // Update total chunk count once known. currentChunkIndex is driven by
    // actual playback start (playNextFromQueue), not by receipt/synthesis —
    // otherwise the highlight (and progress %) races ahead of the audio.
    if (status.totalChunks > 0 && this.totalChunks() === 0) {
      this.totalChunks.set(status.totalChunks);
    }
  }

  /** Decodes base64 audio data into an AudioBuffer and queues it for playback. */
  private async decodeAndQueueBase64Audio(base64: string, chunkIndex: number, text: string): Promise<void> {
    try {
      // Initialize AudioContext lazily
      if (!this.audioContext) {
        this.audioContext = new AudioContext();
      }

      const ctx = this.audioContext;

      // Convert base64 to ArrayBuffer
      const binaryString = atob(base64);
      const bytes = new Uint8Array(binaryString.length);
      for (let i = 0; i < binaryString.length; i++) {
        bytes[i] = binaryString.charCodeAt(i);
      }

      // Decode audio data
      const audioBuffer = await ctx.decodeAudioData(bytes.buffer.slice(0));

      // Queue the buffer with its chunk metadata so the highlight can be
      // driven by actual playback start, not by arrival order.
      this.bufferQueue.push({buffer: audioBuffer, chunkIndex, text});

      // If not currently playing a buffer, start playback from the queue
      if (!this.isPlayingBuffer && !this.isPaused() && !this.streamEnded) {
        this.playNextFromQueue();
      }
    } catch (err) {
      console.error(`Failed to decode audio chunk ${chunkIndex}`, err);
    }
  }

  /** Plays the next AudioBuffer from the queue, chaining via onended callbacks. */
  private playNextFromQueue(): void {
    if (!this.audioContext || this.bufferQueue.length === 0 || this.isPlayingBuffer) return;

    const item = this.bufferQueue.shift();
    if (!item) return;

    // Set the highlight signals here — when audio actually starts playing —
    // so the highlighted sentence stays in sync with the audio.
    this.currentChunkIndex.set(item.chunkIndex);
    this.currentChunkText.set(item.text);

    this.isPlayingBuffer = true;

    const source = this.audioContext.createBufferSource();
    source.buffer = item.buffer;
    source.connect(this.audioContext.destination);

    source.onended = () => {
      this.isPlayingBuffer = false;
      // Play next chunk in the queue (or wait for more to arrive)
      if (this.bufferQueue.length > 0 && !this.streamEnded && !this.isPaused()) {
        this.playNextFromQueue();
      } else if (!this.streamEnded && !this.isPaused()) {
        // Set up a watcher: when new buffers arrive, auto-play them
        this.watchForNewBuffers();
      }
    };

    source.start();
  }

  /** Watches for new buffers to arrive in the queue and auto-plays them. */
  private watchForNewBuffers(): void {
    // Simple polling approach — check periodically if new buffers arrived
    const interval = setInterval(() => {
      if (this.bufferQueue.length > 0 && !this.isPlayingBuffer) {
        clearInterval(interval);
        this.playNextFromQueue();
      } else if (this.streamEnded && this.bufferQueue.length === 0) {
        clearInterval(interval);
      }
    }, 100);

    // Safety timeout to stop polling after stream ends
    setTimeout(() => clearInterval(interval), 600_000);
  }
}
