import {ChangeDetectionStrategy, Component, computed, inject, input, Signal} from '@angular/core';
import {NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {TranslocoDirective, TranslocoPipe} from '@jsverse/transloco';
import {CommonModule, NgClass, NgIf, PercentPipe} from '@angular/common';
import {TtsPlaybackService, TtsPlaybackState} from '../../_services/tts-playback.service';

@Component({
  selector: 'app-tts-controls',
  templateUrl: './tts-controls.component.html',
  styleUrls: ['./tts-controls.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, NgIf, NgClass, PercentPipe, NgbTooltip, TranslocoDirective, TranslocoPipe],
})
export class TtsControlsComponent {

  protected readonly ttsPlayback = inject(TtsPlaybackService);

  /** Whether the controls should be visible (e.g. only when TTS is configured). */
  public readonly visible = input<boolean>(true);

  /** Chapter ID to pass to startStream() when the user clicks play. */
  public readonly chapterId = input<number>(0);

  // Expose playback signals for template access
  protected readonly state: Signal<TtsPlaybackState> = this.ttsPlayback.state;
  protected readonly totalChunks: Signal<number> = this.ttsPlayback.totalChunks;
  protected readonly currentChunkIndex: Signal<number> = this.ttsPlayback.currentChunkIndex;
  protected readonly isPaused: Signal<boolean> = this.ttsPlayback.isPaused;
  protected readonly progressPercent: Signal<number> = this.ttsPlayback.progressPercent;
  protected readonly errorMessage: Signal<string | null> = this.ttsPlayback.errorMessage;

  /** Whether the stream is actively playing (not idle, paused, or error). */
  protected readonly isActive = computed(() => {
    const s = this.state();
    return s === 'playing' || s === 'paused';
  });

  /** Whether we should show a play/pause toggle button. */
  protected readonly canTogglePause = computed(() => {
    const s = this.state();
    return s === 'playing' || s === 'paused';
  });

  /** Whether we should show a stop button. */
  protected readonly canStop = computed(() => {
    const s = this.state();
    return s !== 'idle' && s !== 'completed';
  });

  /** Whether we should show a start/play button (idle or completed state). */
  protected readonly canStart = computed(() => {
    const s = this.state();
    return s === 'idle' || s === 'completed';
  });

  onTogglePause(): void {
    this.ttsPlayback.togglePause();
  }

  onStop(): void {
    this.ttsPlayback.stop();
  }

  onStart(): void {
    this.ttsPlayback.startStream(this.chapterId());
  }
}
