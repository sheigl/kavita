import {ChangeDetectionStrategy, Component, inject, OnInit, signal} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {NgbButtonDirective, NgbDropdown, NgbDropdownItem, NgbDropdownMenu, NgbDropdownToggle, NgbModalRef} from '@ng-bootstrap/ng-bootstrap';
import {TranslocoDirective} from '@jsverse/transloco';
import {NgIf} from '@angular/common';
import {firstValueFrom} from 'rxjs';
import {TtsService} from '../../_services/tts-service';
import {TtsServerConfigDto, TtsVoiceDto} from '../../_models/tts-models';

@Component({
  selector: 'app-manage-tts-config',
  templateUrl: './manage-tts-config.component.html',
  styleUrls: ['./manage-tts-config.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, NgbButtonDirective, NgIf, TranslocoDirective,
    NgbDropdown, NgbDropdownToggle, NgbDropdownMenu, NgbDropdownItem],
})
export class ManageTtsConfigComponent implements OnInit {

  private readonly ttsService = inject(TtsService);
  private readonly modalRef = inject(NgbModalRef, {optional: null});

  // Form state
  public configForm = new FormGroup({
    serverUrl: new FormControl<string>('', [Validators.required, Validators.maxLength(512)]),
    apiKey: new FormControl<string>('', [Validators.maxLength(1024)]),
    defaultModel: new FormControl<string>('tts-1', [Validators.required, Validators.maxLength(256)]),
    defaultVoice: new FormControl<string>('alloy', [Validators.required, Validators.maxLength(256)]),
    defaultSpeed: new FormControl<number>(1.0, [Validators.required, Validators.min(0.5), Validators.max(4.0)]),
  });

  // UI state
  public loading = signal<boolean>(false);
  public saving = signal<boolean>(false);
  public testing = signal<boolean>(false);
  public testResult = signal<{ success: boolean; message: string } | null>(null);
  public voices = signal<TtsVoiceDto[]>([]);

  ngOnInit(): void {
    this.loadConfig();
  }

  async loadConfig(): Promise<void> {
    this.loading.set(true);
    try {
      const config = await firstValueFrom(this.ttsService.getConfig());
      this.configForm.patchValue({
        serverUrl: config.serverUrl,
        apiKey: '', // Never return the actual key for security
        defaultModel: config.defaultModel,
        defaultVoice: config.defaultVoice,
        defaultSpeed: config.defaultSpeed,
      });

      // If we have a configured API key and server URL, load available voices
      if (config.hasApiKey && config.serverUrl) {
        this.loadVoices();
      }
    } catch (err) {
      console.error('Failed to load TTS config', err);
    } finally {
      this.loading.set(false);
    }
  }

  async loadVoices(): Promise<void> {
    try {
      const voices = await firstValueFrom(this.ttsService.getVoices());
      this.voices.set(voices);
    } catch (err) {
      console.error('Failed to load TTS voices', err);
    }
  }

  async onSave(): Promise<void> {
    if (!this.configForm.valid) return;

    const formValue = this.configForm.value;
    this.saving.set(true);
    this.testResult.set(null);

    try {
      await firstValueFrom(this.ttsService.updateConfig({
        serverUrl: formValue.serverUrl!,
        apiKey: formValue.apiKey ?? '',
        defaultModel: formValue.defaultModel!,
        defaultVoice: formValue.defaultVoice!,
        defaultSpeed: formValue.defaultSpeed!,
      }));

      // Reload voices after saving
      await this.loadVoices();
    } catch (err) {
      console.error('Failed to save TTS config', err);
    } finally {
      this.saving.set(false);
    }
  }

  async onTest(): Promise<void> {
    if (!this.configForm.valid) return;

    const formValue = this.configForm.value;
    this.testing.set(true);
    this.testResult.set(null);

    try {
      const result = await firstValueFrom(this.ttsService.testConnection({
        serverUrl: formValue.serverUrl!,
        apiKey: formValue.apiKey ?? '',
        model: formValue.defaultModel!,
        voice: formValue.defaultVoice!,
      }));
      this.testResult.set(result);
    } catch (err) {
      console.error('TTS test failed', err);
      this.testResult.set({ success: false, message: 'Connection failed' });
    } finally {
      this.testing.set(false);
    }
  }

  onClose(): void {
    this.modalRef?.close();
  }

  protected readonly Validators = Validators;
}
