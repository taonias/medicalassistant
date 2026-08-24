import type {
  ParsedStructuredSummary,
  StructuredDataField,
  StructuredDataSection,
} from '../../features/medical-data';

function fieldFromEntry(
  key: string,
  value: unknown,
): StructuredDataField | null {
  if (value === null || value === undefined) {
    return null;
  }

  if (typeof value === 'object' && !Array.isArray(value)) {
    return null;
  }

  return {
    key,
    label: key.replace(/([A-Z])/g, ' $1').replace(/^./, (s) => s.toUpperCase()),
    value: Array.isArray(value) ? value.join(', ') : (value as string | number | boolean),
  };
}

function sectionFromArray(label: string, type: string, items: unknown[]): StructuredDataSection {
  const fields: StructuredDataField[] = [];

  items.forEach((item, index) => {
    if (typeof item === 'object' && item !== null && !Array.isArray(item)) {
      Object.entries(item).forEach(([key, value]) => {
        const field = fieldFromEntry(`${index + 1}.${key}`, value);
        if (field) fields.push(field);
      });
    } else {
      const field = fieldFromEntry(String(index + 1), item);
      if (field) fields.push(field);
    }
  });

  return { type, label, fields };
}

export function parseStructuredSummary(
  structuredSummary?: string,
): ParsedStructuredSummary {
  if (!structuredSummary) {
    return { sections: [] };
  }

  try {
    const parsed = JSON.parse(structuredSummary) as Record<string, unknown>;
    const sections: StructuredDataSection[] = [];

    if (typeof parsed.summary === 'string') {
      sections.push({
        type: 'summary',
        label: 'Summary',
        fields: [{ key: 'summary', label: 'Summary', value: parsed.summary }],
      });
    }

    Object.entries(parsed).forEach(([key, value]) => {
      if (key === 'summary') return;

      if (Array.isArray(value)) {
        sections.push(
          sectionFromArray(
            key.replace(/([A-Z])/g, ' $1').replace(/^./, (s) => s.toUpperCase()),
            key,
            value,
          ),
        );
        return;
      }

      const field = fieldFromEntry(key, value);
      if (field) {
        sections.push({
          type: key,
          label: field.label,
          fields: [field],
        });
      }
    });

    return {
      summary: typeof parsed.summary === 'string' ? parsed.summary : undefined,
      sections,
    };
  } catch {
    return {
      summary: structuredSummary,
      sections: [
        {
          type: 'summary',
          label: 'Summary',
          fields: [{ key: 'summary', label: 'Summary', value: structuredSummary }],
        },
      ],
    };
  }
}
