import { DownloadIcon } from '../../../../app/shell/navigation/NavIcons';
import type { ApiError } from '../../../../shared/types/api';
import { consultationApi } from '../../api/consultationApi';

interface Props {
  consultationId: number;
  documentFileName: string;
}

export function DocumentPanel({ consultationId, documentFileName }: Props) {
  return (
    <section className="panel">
      <div className="panel-heading">
        <h3>Document</h3>
        <button
          type="button"
          className="icon-button"
          aria-label="Download PDF"
          title="Download PDF"
          onClick={() => {
            void consultationApi
              .downloadDocument(consultationId, documentFileName)
              .catch((error: ApiError) => {
                window.alert(error.message ?? 'Unable to download document.');
              });
          }}
        >
          <DownloadIcon />
        </button>
      </div>
      <p>{documentFileName}</p>
    </section>
  );
}
