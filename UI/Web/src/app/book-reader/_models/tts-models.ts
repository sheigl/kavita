/**
 * TTS server configuration returned by /api/tts/config (GET) and sent to /api/tts/config (PUT).
 */
export interface TtsServerConfigDto {
  /** Base URL of the configured TTS server (e.g. "https://api.openai.com"). */
  serverUrl: string;
  /** Whether an API key is currently configured (not the actual key). */
  hasApiKey: boolean;
  /** Default TTS model identifier. */
  defaultModel: string;
  /** Default voice identifier. */
  defaultVoice: string;
  /** Default playback speed multiplier (0.5 - 4.0). */
  defaultSpeed: number;
}

/**
 * Request body for PUT /api/tts/config to update the user's TTS server configuration.
 */
export interface TtsServerConfigRequest {
  /** Base URL of the TTS server. */
  serverUrl: string;
  /** API key for authentication (plain text, encrypted at rest). */
  apiKey: string;
  /** TTS model to use (e.g. "tts-1"). */
  defaultModel: string;
  /** Default voice identifier (e.g. "alloy"). */
  defaultVoice: string;
  /** Default playback speed multiplier (0.5 - 4.0). */
  defaultSpeed: number;
}

/**
 * Represents a voice option returned by GET /api/tts/voices.
 */
export interface TtsVoiceDto {
  /** Unique identifier for this voice (e.g. "alloy", "nova"). */
  voiceId: string;
  /** Human-readable name of the voice. */
  name: string;
}

/**
 * Request body to start a TTS streaming session via POST /api/tts/stream/{chapterId}.
 */
export interface TtsStreamRequest {
  /** Zero-based index of the first chunk to synthesize. */
  startChunk: number;
  /** Zero-based index of the last chunk (inclusive). Use -1 for "all remaining". */
  endChunk: number;
  /** Granularity used when chunks were generated. */
  granularity: TtsChunkGranularity;
  /** Optional voice override for this session. Falls back to profile/user defaults if null. */
  voiceOverride?: string | null;
  /** Optional speed override (0.5 - 4.0) for this session. Falls back to profile/user defaults if null. */
  speedOverride?: number | null;
}

/**
 * Granularity level for text chunking when sending text to the TTS server.
 */
export enum TtsChunkGranularity {
  /** Split at every paragraph break. */
  Paragraph = 0,
  /** Split at every sentence boundary (period, question mark, exclamation). */
  Sentence = 1,
}

/**
 * Request body for POST /api/tts/test to test TTS server connectivity.
 */
export interface TtsTestRequestDto {
  /** Base URL of the TTS server to test. */
  serverUrl: string;
  /** API key for authentication. */
  apiKey: string;
  /** TTS model to use (e.g. "tts-1"). */
  model: string;
  /** Voice identifier to use for the test (e.g. "alloy"). */
  voice: string;
}

/**
 * Response from POST /api/tts/test.
 */
export interface TtsTestResponseDto {
  /** Whether the test request succeeded (HTTP 200 + valid audio returned). */
  success: boolean;
  /** Human-readable message describing the result. */
  message: string;
}

/**
 * Single text chunk extracted from an EPUB chapter.
 */
export interface TextChunkDto {
  /** Zero-based index of this chunk in the full list. */
  index: number;
  /** The text content for this chunk (trimmed, non-empty). */
  text: string;
}

/**
 * Response containing all text chunks extracted from an EPUB chapter via GET /api/tts/text-chunks/{chapterId}.
 */
export interface TextChunksResponseDto {
  /** List of text chunks in reading order. */
  chunks: TextChunkDto[];
  /** Total number of chunks returned. */
  totalChunks: number;
}

/**
 * SignalR message containing a base64-encoded audio chunk (TtsAudioChunk event).
 */
export interface TtsAudioChunkMessage {
  /** Zero-based index of this chunk in the stream. */
  chunkIndex: number;
  /** Base64-encoded audio data for this chunk. */
  audioBase64: string;
  /** The original text content for this chunk, used for read-along highlighting. */
  text: string;
}

/**
 * SignalR message containing stream progress/status updates (TtsPlaybackUpdate event).
 */
export interface TtsStreamStatusMessage {
  /** Zero-based index of the current chunk being processed. */
  chunkIndex: number;
  /** Total number of chunks in this stream session. */
  totalChunks: number;
  /** Whether the stream has completed successfully. */
  isComplete: boolean;
  /** Whether the stream was cancelled by the user. */
  isCancelled: boolean;
  /** Error message if the stream failed (null on success). */
  errorMessage?: string | null;
}
