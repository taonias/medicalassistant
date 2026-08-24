export interface AuthRequest {
  userName: string;
  password: string;
}

export interface AuthResponse {
  id: string;
  userName?: string;
  email?: string;
  emailConfirmed: boolean;
  firstName?: string;
  lastName?: string;
  token: string;
  roles: string[];
}

export interface UserSession {
  id: string;
  userName: string;
  email: string;
  emailConfirmed: boolean;
  firstName: string;
  lastName: string;
}
