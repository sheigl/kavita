import {ChangeDetectorRef, signal} from '@angular/core';
import {ComponentFixture, TestBed, fakeAsync, tick} from '@angular/core/testing';
import {TtsControlsComponent} from './tts-controls.component';
import {TtsPlaybackService, TtsPlaybackState} from '../../_services/tts-playback.service';

describe('TtsControlsComponent', () => {
  let component: TtsControlsComponent;
  let fixture: ComponentFixture<TtsControlsComponent>;
  let mockPlayback: jasmine.SpyObj<TtsPlaybackService>;

  beforeEach(async () => {
    const playbackSpy = jasmine.createSpyObj('TtsPlaybackService', [
      'togglePause',
      'stop',
    ]);

    // Set up default signal values
    Object.defineProperty(playbackSpy, 'state', {
      get: () => signal<TtsPlaybackState>('idle'),
    });
    Object.defineProperty(playbackSpy, 'totalChunks', {
      get: () => signal<number>(10),
    });
    Object.defineProperty(playbackSpy, 'currentChunkIndex', {
      get: () => signal<number>(3),
    });
    Object.defineProperty(playbackSpy, 'isPaused', {
      get: () => signal<boolean>(false),
    });
    Object.defineProperty(playbackSpy, 'progressPercent', {
      get: () => signal<number>(30),
    });
    Object.defineProperty(playbackSpy, 'errorMessage', {
      get: () => signal<string | null>(null),
    });

    await TestBed.configureTestingModule({
      imports: [TtsControlsComponent],
      providers: [
        {provide: TtsPlaybackService, useValue: playbackSpy},
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(TtsControlsComponent);
    component = fixture.componentInstance;
    mockPlayback = TestBed.inject(TtsPlaybackService) as jasmine.SpyObj<TtsPlaybackService>;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('calls togglePause when onTogglePause is called', () => {
    component.onTogglePause();
    expect(mockPlayback.togglePause).toHaveBeenCalled();
  });

  it('calls stop when onStop is called', () => {
    component.onStop();
    expect(mockPlayback.stop).toHaveBeenCalled();
  });
});
