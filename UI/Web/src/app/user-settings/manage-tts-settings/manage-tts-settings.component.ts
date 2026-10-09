import {ChangeDetectionStrategy, Component, inject, signal} from '@angular/core';
import {AsyncPipe} from '@angular/common';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {TtsService} from '../../book-reader/_services/tts-service';
import {TtsServerConfigDto, TtsServerConfigRequest, TtsTestRequestDto, TtsTestResponseDto, TtsVoiceDto} from '../../book-reader/_models/tts-models';
import {firstValueFrom, Observable, of} from 'rxjs';
import {catchError} from 'rxjs/operators';
import {TranslocoModule} from '@jsverse/transloco';

@Component({
  selector: 'app-manage-tts-settings',
  imports: [ReactiveFormsModule, TranslocoModule, AsyncPipe],
  templateUrl: './manage-tts-settings.component.html',
  styleUrl: './manage-tts-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageTtsSettingsComponent {
  private readonly ttsService = inject(TtsService);

  protected readonly loading = signal<boolean>(true);
  protected readonly testing = signal<boolean>(false);
  protected readonly saving = signal<boolean>(false);
  protected readonly testResult = signal<TtsTestResponseDto | null>(null);

  /** Voices available on the user's TTS server (falls back to OpenAI defaults). */
  protected readonly voices$ = this.ttsService.getVoices().pipe(
    catchError(() => of<TtsVoiceDto[]>([])),
  );

  protected configForm = new FormGroup({
    serverUrl: new FormControl<string>('', [Validators.required, Validators.maxLength(512)]),
    apiKey: new FormControl<string>('', [Validators.maxLength(1024)]),
    defaultModel: new FormControl<string>('tts-1', [Validators.required, Validators.maxLength(256)]),
    defaultVoice: new FormControl<string>('alloy', [Validators.required, Validators.maxLength(256)]),
    defaultSpeed: new FormControl<number>(1.0, [Validators.required, Validators.min(0.5), Validators.max(4.0)]),
  });

  public saveError = signal<string | null>(null);

  constructor() {
    this.loadConfig();
  }

  private async loadConfig() {
    try {
      const config = await firstValueFrom(this.ttsService.getConfig()) as TtsServerConfigDto | undefined;
      if (config) {
        this.configForm.patchValue({
          serverUrl: config.serverUrl ?? '',
          apiKey: '',
          defaultModel: config.defaultModel ?? 'tts-1',
          defaultVoice: config.defaultVoice ?? 'alloy',
          defaultSpeed: config.defaultSpeed ?? 1.0,
        });
      }
    } finally {
      this.loading.set(false);
    }
  }

   protected async onTest() {
    if (this.configForm.invalid) {
      return;
    }

    this.testing.set(true);
    this.testResult.set(null);

    try {
      const testRequest: TtsTestRequestDto = {
        serverUrl: this.configForm.get('serverUrl')?.value ?? '',
        apiKey: this.configForm.get('apiKey')?.value ?? '',
        model: this.configForm.get('defaultModel')?.value ?? 'tts-1',
        voice: this.configForm.get('defaultVoice')?.value ?? 'alloy',
      };
      const result = await firstValueFrom(this.ttsService.testConnection(testRequest)) as TtsTestResponseDto | undefined;
      this.testResult.set(result ?? { success: false, message: 'Unknown error' });
    } catch (err) {
      this.testResult.set({ success: false, message: (err as Error)?.message ?? 'Test failed' });
    } finally {
      this.testing.set(false);
    }
  }

  protected async onSave() {
    if (this.configForm.invalid) {
      return;
    }

    this.saving.set(true);
    this.testResult.set(null);
    this.saveError.set(null);

    try {
      const request = this.buildRequest();
      await firstValueFrom(this.ttsService.updateConfig(request));
    } catch (err: any) {
      this.saveError.set((err as Error)?.message ?? 'Failed to save configuration');
    } finally {
      this.saving.set(false);
    }
  }

  private buildRequest(): TtsServerConfigRequest {
    return {
      serverUrl: this.configForm.get('serverUrl')?.value ?? '',
      apiKey: this.configForm.get('apiKey')?.value ?? '',
      defaultModel: this.configForm.get('defaultModel')?.value ?? 'tts-1',
      defaultVoice: this.configForm.get('defaultVoice')?.value ?? 'alloy',
      defaultSpeed: this.configForm.get('defaultSpeed')?.value ?? 1.0,
    };
  }
}
