import { SearchIcon } from '../../../app/shell/navigation/NavIcons';

interface Props {
  value: string;
  onChange: (value: string) => void;
}

export function PatientSearchField({ value, onChange }: Props) {
  return (
    <label className="patient-search">
      <SearchIcon />
      <input
        type="search"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder="Search patients..."
        aria-label="Search patients"
      />
    </label>
  );
}
