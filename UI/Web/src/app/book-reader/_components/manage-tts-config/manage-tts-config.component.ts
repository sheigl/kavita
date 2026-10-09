import {ChangeDetectionStrategy, Component, inject, OnInit, signal} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {NgbActiveModal, NgbDropdown, NgbDropdownItem, NgbDropdownMenu, NgbDropdownToggle} from '@ng-bootstrap/ng-bootstrap';
import {TranslocoDirective, TranslocoPipe} from '@jsverse/transloco';
import {NgIf} from '@angular/common';
import {firstValueFrom} from 'rxjs';
import {TtsService} from '../../_services/tts-service';
import {TtsServerConfigDto, TtsVoiceDto} from '../../_models/tts-models';

@Component({
  selector: 'app-manage-tts-config',
  templateUrl: './manage-tts-config.component.html',
  styleUrls: ['./manage-tts-config.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, NgIf, TranslocoDirective, TranslocoPipe,
    NgbDropdown, NgbDropdownToggle, NgbDropdownMenu, NgbDropdownItem],
})
export class ManageTtsConfigComponent implements OnInit {

  private readonly ttsService = inject(TtsService);
  // NgbActiveModal is the correct NGBprovided service for modal *content*
  // components to control their own dismiss/close. NgbModalRef is only the
  // return value of modalService.open() (meant for the opener); injecting it
  // here returns null and onClose() silently no-ops, leaving the modal stuck.
  private readonly modalRef = inject(NgbActiveModal);

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
  // Visible error string when the most recent Save attempt failed.
  // Cleared on every new save attempt, set on error.
  public saveError = signal<string | null>(null);

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
    if (!this.configForm.valid) {
      this.saveError.set('Please fill in all required fields.');
      this.configForm.markAllAsTouched();
      return;
    }

    const formValue = this.configForm.value;
    this.saving.set(true);
    this.testResult.set(null);
    this.saveError.set(null);

    try {
      // NOTE: backend treats an empty apiKey as "preserve existing key"
      // (see SaveUserTtsConfigAsync), so it's safe to send an empty string
      // when the user hasn't re-entered their key — that no longer wipes
      // the stored encrypted key.
      await firstValueFrom(this.ttsService.updateConfig({
        serverUrl: formValue.serverUrl!,
        apiKey: formValue.apiKey ?? '',
        defaultModel: formValue.defaultModel!,
        defaultVoice: formValue.defaultVoice!,
        defaultSpeed: formValue.defaultSpeed!,
      }));

      // Reload voices after saving (newly-configured server may expose voices)
      await this.loadVoices();

      // Close the modal on successful save so the user gets clear feedback
      // (previously it silently stayed open with no indication of success).
      this.modalRef.close('saved');
    } catch (err: any) {
      const msg = (err as Error)?.message
        ?? (err as any)?.error?.message
        ?? (typeof err === 'string' ? err : 'Failed to save configuration');
      this.saveError.set(msg);
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
