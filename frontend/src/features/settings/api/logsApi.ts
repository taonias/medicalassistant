import { httpClient } from '../../../shared/api/httpClient';
import type { AuditLogEntry, ErrorLogEntry, PagedResult } from '../../../shared/types/api';

export type LogsQueryParams = {
  page: number;
  pageSize: number;
};

function withPaging(path: string, params: LogsQueryParams) {
  const search = new URLSearchParams({
    page: String(params.page),
    pageSize: String(params.pageSize),
  });
  return `${path}?${search}`;
}

export const logsApi = {
  audit: (params: LogsQueryParams) =>
    httpClient<PagedResult<AuditLogEntry>>(withPaging('/logs/audit', params)),

  errors: (params: LogsQueryParams) =>
    httpClient<PagedResult<ErrorLogEntry>>(withPaging('/logs/errors', params)),
};
