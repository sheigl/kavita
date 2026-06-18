import {HttpClient} from '@angular/common/http';
import {inject, Injectable} from '@angular/core';
import {environment} from 'src/environments/environment';
import {
  TextChunksResponseDto,
  TtsServerConfigDto,
  TtsServerConfigRequest,
  TtsStreamRequest,
  TtsTestRequestDto,
  TtsTestResponseDto,
  TtsVoiceDto,
} from '../_models/tts-models';

@Injectable({
  providedIn: 'root'
})
export class TtsService {

  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  /** GET /api/tts/config — Returns the user's current TTS server configuration. */
  getConfig() {
    return this.http.get<TtsServerConfigDto>(this.baseUrl + 'tts/config');
  }

  /** PUT /api/tts/config — Updates the user's TTS server configuration. */
  updateConfig(config: TtsServerConfigRequest) {
    return this.http.put<TtsServerConfigDto>(this.baseUrl + 'tts/config', config);
  }

  /** POST /api/tts/test — Tests connectivity to the configured TTS server. */
  testConnection(request: TtsTestRequestDto) {
    return this.http.post<TtsTestResponseDto>(this.baseUrl + 'tts/test', request);
  }

  /** GET /api/tts/voices — Lists available voices from the TTS server. */
  getVoices() {
    return this.http.get<TtsVoiceDto[]>(this.baseUrl + 'tts/voices');
  }

  /** GET /api/tts/chunks?chapterId={id}&granularity=... — Extracts text chunks from an EPUB chapter. */
  getTextChunks(chapterId: number, granularity?: string) {
    const params: Record<string, string> = { chapterId: chapterId.toString() };
    if (granularity) {
      params.granularity = granularity;
    }
    return this.http.get<TextChunksResponseDto>(`${this.baseUrl}tts/chunks`, { params });
  }

  /** POST /api/tts/stream?chapterId={id} — Starts a SignalR audio streaming session for the chapter. */
  startStream(chapterId: number, request: TtsStreamRequest) {
    return this.http.post<void>(`${this.baseUrl}tts/stream`, request, {
      params: { chapterId: chapterId.toString() },
    });
  }

  /** POST /api/tts/stream/stop — Stops the active TTS stream for the current user. */
  stopStream() {
    return this.http.post<void>(this.baseUrl + 'tts/stream/stop', {});
  }
}
