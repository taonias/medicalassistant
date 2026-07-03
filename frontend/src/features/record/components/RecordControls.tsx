import { PauseIcon, PlayIcon, StopIcon } from '../../../layouts/navigation/NavIcons';

interface Props {
  isPaused: boolean;
  onPause: () => void;
  onResume: () => void;
  onStop: () => void;
}

export function RecordControls({ isPaused, onPause, onResume, onStop }: Props) {
  return (
    <div className="record-controls">
      <button
        type="button"
        className="icon-button record-controls__button"
        onClick={isPaused ? onResume : onPause}
        aria-label={isPaused ? 'Resume recording' : 'Pause recording'}
        title={isPaused ? 'Resume' : 'Pause'}
      >
        {isPaused ? <PlayIcon /> : <PauseIcon />}
      </button>
      <button
        type="button"
        className="icon-button icon-button--danger record-controls__button record-controls__button--stop"
        onClick={onStop}
        aria-label="Stop recording"
        title="Stop"
      >
        <StopIcon />
      </button>
    </div>
  );
}
