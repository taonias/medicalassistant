import type { ParsedStructuredSummary } from '../types';

interface Props {
  data: ParsedStructuredSummary;
}

export function StructuredDataPanel({ data }: Props) {
  if (data.sections.length === 0) {
    return (
      <div className="panel">
        <h3>Structured medical data</h3>
        <p className="muted">No structured data extracted yet.</p>
      </div>
    );
  }

  return (
    <div className="panel">
      <h3>Structured medical data</h3>
      <div className="structured-data">
        {data.sections.map((section) => (
          <section key={section.type} className="structured-data__section">
            <h4>{section.label}</h4>
            <dl className="structured-data__fields">
              {section.fields.map((field) => (
                <div key={field.key} className="structured-data__field">
                  <dt>{field.label}</dt>
                  <dd>{String(field.value ?? '—')}</dd>
                </div>
              ))}
            </dl>
          </section>
        ))}
      </div>
    </div>
  );
}
