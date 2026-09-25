import { httpClient } from '../../../shared/api/httpClient';
import type { ManagedUser, PagedResult, SetUserApprovalRequest } from '../../../shared/types/api';

export type UsersQueryParams = {
  page: number;
  pageSize: number;
};

export const usersApi = {
  list: (params: UsersQueryParams) => {
    const search = new URLSearchParams({
      page: String(params.page),
      pageSize: String(params.pageSize),
    });
    return httpClient<PagedResult<ManagedUser>>(`/users?${search}`);
  },

  setApproval: (id: string, request: SetUserApprovalRequest) =>
    httpClient<void>(`/users/${id}/approval`, {
      method: 'PUT',
      body: request,
    }),
};
