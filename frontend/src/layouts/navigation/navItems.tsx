import type { ReactNode } from 'react';
import {
  ChatIcon,
  DashboardIcon,
  PatientsIcon,
  RecordIcon,
  SettingsIcon,
} from './NavIcons';

export interface NavItem {
  id: string;
  label: string;
  to: string;
  icon: ReactNode;
  end?: boolean;
  /** Center FAB — only used in mobile footer */
  isPrimary?: boolean;
}

export const mainNavItems: NavItem[] = [
  {
    id: 'dashboard',
    label: 'Dashboard',
    to: '/',
    icon: <DashboardIcon />,
    end: true,
  },
  {
    id: 'patients',
    label: 'Patients',
    to: '/patients',
    icon: <PatientsIcon />,
  },
  {
    id: 'record',
    label: 'Record',
    to: '/record',
    icon: <RecordIcon />,
    isPrimary: true,
  },
  {
    id: 'chat',
    label: 'Chat',
    to: '/chat',
    icon: <ChatIcon />,
  },
  {
    id: 'settings',
    label: 'Settings',
    to: '/settings',
    icon: <SettingsIcon />,
  },
];

export const desktopNavItems = mainNavItems.filter((item) => !item.isPrimary);
