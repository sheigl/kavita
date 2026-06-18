import {ComponentFixture, TestBed} from '@angular/core/testing';
import {ManageTtsConfigComponent} from './manage-tts-config.component';
import {NgbModalRef} from '@ng-bootstrap/ng-bootstrap';
import {TtsService} from '../../_services/tts-service';
import {of} from 'rxjs';

describe('ManageTtsConfigComponent', () => {
  let component: ManageTtsConfigComponent;
  let fixture: ComponentFixture<ManageTtsConfigComponent>;
  let mockTtsService: jasmine.SpyObj<TtsService>;

  beforeEach(async () => {
    mockTtsService = jasmine.createSpyObj('TtsService', [
      'getConfig',
      'updateConfig',
      'testConnection',
      'getVoices',
    ]);

    // Set up default return values
    mockTtsService.getConfig.and.returnValue(of({
      serverUrl: 'https://api.openai.com',
      hasApiKey: true,
      defaultModel: 'tts-1',
      defaultVoice: 'alloy',
      defaultSpeed: 1.0,
    }));

    mockTtsService.getVoices.and.returnValue(of([
      {voiceId: 'alloy', name: 'Alloy'},
      {voiceId: 'nova', name: 'Nova'},
    ]));

    await TestBed.configureTestingModule({
      imports: [ManageTtsConfigComponent],
      providers: [
        {provide: TtsService, useValue: mockTtsService},
        {provide: NgbModalRef, useValue: null},
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ManageTtsConfigComponent);
    component = fixture.componentInstance;
    mockTtsService = TestBed.inject(TtsService) as jasmine.SpyObj<TtsService>;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('loads config on init', () => {
    component.ngOnInit();
    fixture.detectChanges();
    expect(mockTtsService.getConfig).toHaveBeenCalled();
  });

  it('calls ttsService.updateConfig when onSave is called with valid form', async () => {
    component.configForm.patchValue({
      serverUrl: 'https://api.openai.com',
      apiKey: 'test-key',
      defaultModel: 'tts-1',
      defaultVoice: 'alloy',
      defaultSpeed: 1.0,
    });

    mockTtsService.updateConfig.and.returnValue(of({}));

    await component.onSave();
    expect(mockTtsService.updateConfig).toHaveBeenCalled();
  });

  it('does not call updateConfig when form is invalid', async () => {
    component.configForm.patchValue({
      serverUrl: '', // Invalid - required field empty
      apiKey: '',
      defaultModel: 'tts-1',
      defaultVoice: 'alloy',
      defaultSpeed: 1.0,
    });

    await component.onSave();
    expect(mockTtsService.updateConfig).not.toHaveBeenCalled();
  });

  it('calls ttsService.testConnection when onTest is called with valid form', async () => {
    component.configForm.patchValue({
      serverUrl: 'https://api.openai.com',
      apiKey: 'test-key',
      defaultModel: 'tts-1',
      defaultVoice: 'alloy',
      defaultSpeed: 1.0,
    });

    mockTtsService.testConnection.and.returnValue(of({success: true, message: 'OK'}));

    await component.onTest();
    expect(mockTtsService.testConnection).toHaveBeenCalled();
  });

  it('closes modal when onClose is called', () => {
    const mockModalRef = jasmine.createSpyObj('NgbModalRef', ['close']);
    (component as any).modalRef = mockModalRef;

    component.onClose();
    expect(mockModalRef.close).toHaveBeenCalled();
  });
});
