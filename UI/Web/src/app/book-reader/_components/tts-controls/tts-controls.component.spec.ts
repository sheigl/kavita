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
      'startStream',
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

  // --- Play button visibility tests (canStart computed signal) ---

  describe('canStart play button visibility', () => {
    it('should show play button when state is idle', () => {
      expect(component.canStart()).toBeTrue();
    });

    it('should show play button when state is completed', () => {
      // Simulate completed state by reconfiguring the mock
      const playbackSpy = jasmine.createSpyObj('TtsPlaybackService', [
        'togglePause',
        'stop',
        'startStream',
      ]);
      Object.defineProperty(playbackSpy, 'state', {
        get: () => signal<TtsPlaybackState>('completed'),
      });

      TestBed.overrideProvider(TtsPlaybackService, {useValue: playbackSpy});
      component = TestBed.inject(TtsControlsComponent);
      
      expect(component.canStart()).toBeTrue();
    });

    it('should NOT show play button when state is playing', () => {
      const playbackSpy = jasmine.createSpyObj('TtsPlaybackService', [
        'togglePause',
        'stop',
        'startStream',
      ]);
      Object.defineProperty(playbackSpy, 'state', {
        get: () => signal<TtsPlaybackState>('playing'),
      });

      TestBed.overrideProvider(TtsPlaybackService, {useValue: playbackSpy});
      component = TestBed.inject(TtsControlsComponent);
      
      expect(component.canStart()).toBeFalse();
    });

    it('should NOT show play button when state is paused', () => {
      const playbackSpy = jasmine.createSpyObj('TtsPlaybackService', [
        'togglePause',
        'stop',
        'startStream',
      ]);
      Object.defineProperty(playbackSpy, 'state', {
        get: () => signal<TtsPlaybackState>('paused'),
      });

      TestBed.overrideProvider(TtsPlaybackService, {useValue: playbackSpy});
      component = TestBed.inject(TtsControlsComponent);
      
      expect(component.canStart()).toBeFalse();
    });

    it('should NOT show play button when state is connecting', () => {
      const playbackSpy = jasmine.createSpyObj('TtsPlaybackService', [
        'togglePause',
        'stop',
        'startStream',
      ]);
      Object.defineProperty(playbackSpy, 'state', {
        get: () => signal<TtsPlaybackState>('connecting'),
      });

      TestBed.overrideProvider(TtsPlaybackService, {useValue: playbackSpy});
      component = TestBed.inject(TtsControlsComponent);
      
      expect(component.canStart()).toBeFalse();
    });

    it('should NOT show play button when state is error', () => {
      const playbackSpy = jasmine.createSpyObj('TtsPlaybackService', [
        'togglePause',
        'stop',
        'startStream',
      ]);
      Object.defineProperty(playbackSpy, 'state', {
        get: () => signal<TtsPlaybackState>('error'),
      });

      TestBed.overrideProvider(TtsPlaybackService, {useValue: playbackSpy});
      component = TestBed.inject(TtsControlsComponent);
      
      expect(component.canStart()).toBeFalse();
    });
  });

  // --- onStart method tests ---

  describe('onStart play action', () => {
    it('should call startStream with correct chapterId when clicked', () => {
      component.chapterId.set(42);
      
      component.onStart();
      
      expect(mockPlayback.startStream).toHaveBeenCalledWith(42);
    });

    it('should pass 0 as chapterId if not provided (default)', () => {
      // Default chapterId is 0 when not set
      component.chapterId.set(0);
      
      component.onStart();
      
      expect(mockPlayback.startStream).toHaveBeenCalledWith(0);
    });
  });

  // --- Error recovery flow tests ---

  describe('error state recovery', () => {
    it('should show stop button when in error state (allows user to reset)', () => {
      const playbackSpy = jasmine.createSpyObj('TtsPlaybackService', [
        'togglePause',
        'stop',
        'startStream',
      ]);
      Object.defineProperty(playbackSpy, 'state', {
        get: () => signal<TtsPlaybackState>('error'),
      });

      TestBed.overrideProvider(TtsPlaybackService, {useValue: playbackSpy});
      component = TestBed.inject(TtsControlsComponent);
      
      // canStop should be true for error state so user can reset
      expect(component.canStop()).toBeTrue();
    });

    it('should show play button after stop resets to idle', () => {
      const playbackSpy = jasmine.createSpyObj('TtsPlaybackService', [
        'togglePause',
        'stop',
        'startStream',
      ]);
      Object.defineProperty(playbackSpy, 'state', {
        get: () => signal<TtsPlaybackState>('idle'),
      });

      TestBed.overrideProvider(TtsPlaybackService, {useValue: playbackSpy});
      component = TestBed.inject(TtsControlsComponent);
      
      // After stopAndReset(), state is idle → play button should appear
      expect(component.canStart()).toBeTrue();
    });
  });

  // --- canTogglePause tests ---

  describe('canTogglePause visibility', () => {
    it('should show pause toggle when playing', () => {
      const playbackSpy = jasmine.createSpyObj('TtsPlaybackService', [
        'togglePause',
        'stop',
        'startStream',
      ]);
      Object.defineProperty(playbackSpy, 'state', {
        get: () => signal<TtsPlaybackState>('playing'),
      });

      TestBed.overrideProvider(TtsPlaybackService, {useValue: playbackSpy});
      component = TestBed.inject(TtsControlsComponent);
      
      expect(component.canTogglePause()).toBeTrue();
    });

    it('should show pause toggle when paused', () => {
      const playbackSpy = jasmine.createSpyObj('TtsPlaybackService', [
        'togglePause',
        'stop',
        'startStream',
      ]);
      Object.defineProperty(playbackSpy, 'state', {
        get: () => signal<TtsPlaybackState>('paused'),
      });

      TestBed.overrideProvider(TtsPlaybackService, {useValue: playbackSpy});
      component = TestBed.inject(TtsControlsComponent);
      
      expect(component.canTogglePause()).toBeTrue();
    });

    it('should NOT show pause toggle when idle', () => {
      expect(component.canTogglePause()).toBeFalse();
    });
  });

  // --- canStop tests ---

  describe('canStop visibility', () => {
    it('should NOT show stop button when idle', () => {
      expect(component.canStop()).toBeFalse();
    });

    it('should NOT show stop button when completed', () => {
      const playbackSpy = jasmine.createSpyObj('TtsPlaybackService', [
        'togglePause',
        'stop',
        'startStream',
      ]);
      Object.defineProperty(playbackSpy, 'state', {
        get: () => signal<TtsPlaybackState>('completed'),
      });

      TestBed.overrideProvider(TtsPlaybackService, {useValue: playbackSpy});
      component = TestBed.inject(TtsControlsComponent);
      
      expect(component.canStop()).toBeFalse();
    });

    it('should show stop button when playing', () => {
      const playbackSpy = jasmine.createSpyObj('TtsPlaybackService', [
        'togglePause',
        'stop',
        'startStream',
      ]);
      Object.defineProperty(playbackSpy, 'state', {
        get: () => signal<TtsPlaybackState>('playing'),
      });

      TestBed.overrideProvider(TtsPlaybackService, {useValue: playbackSpy});
      component = TestBed.inject(TtsControlsComponent);
      
      expect(component.canStop()).toBeTrue();
    });
  });
});
