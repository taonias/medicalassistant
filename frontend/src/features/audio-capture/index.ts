// Public surface of the audio-capture feature.
export { getAudioDurationSeconds } from './utils/getAudioDuration';
export {
  buildRecordingFile,
  preferredRecordingMimeType,
  setMicrophoneEnabled,
} from './utils/recordingMedia';
export { AudioUploader, isPdfFile } from './components/AudioUploader';
export { UploadProgress } from './components/UploadProgress';
export { NewConsultationPage } from './pages/NewConsultationPage';
