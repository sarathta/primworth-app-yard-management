import React, { createContext, useContext, useMemo } from 'react';
import { getToken, getUserData } from '../../../../auth';

/**
 * mSYMS's auth context, re-backed by the platform.
 *
 * The original kept its own token in localStorage under `msyms_token` and had its own
 * Login screen. Inside the platform the user has already signed in, so this projects the
 * platform session into the exact shape the mSYMS screens expect. Same exported names and
 * same fields, so no screen needed editing.
 *
 * login/logout remain on the interface because the type demands them, but they are no-ops:
 * session lifecycle belongs to the platform, and mSYMS's Login screen is not mounted.
 */
interface User {
  id: string;
  username: string;
  role: string;
  roles: string[];
}

interface AuthContextType {
  user: User | null;
  token: string | null;
  login: (token: string, user: User) => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextType>({
  user: null,
  token: null,
  login: () => {},
  logout: () => {},
});

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const value = useMemo<AuthContextType>(() => {
    const platformUser = getUserData();

    // mSYMS gates a few actions on role names (notably QA Admin for releasing a LIMS lock).
    // The platform expresses the same thing as permission codes, so both the role name and
    // the permission codes are exposed and the screens can match on either.
    const permissionCodes: string[] = Array.isArray(platformUser?.permissions)
      ? platformUser.permissions.map((p: { permission_code?: string }) => p?.permission_code).filter(Boolean)
      : [];

    const roleName = platformUser?.role_name
      || (platformUser?.is_super_admin ? 'System Admin' : 'Yard Operator');

    return {
      token: getToken(),
      user: platformUser
        ? {
            id: String(platformUser.id ?? ''),
            username: [platformUser.first_name, platformUser.last_name].filter(Boolean).join(' ')
              || platformUser.email
              || 'User',
            role: roleName,
            roles: [roleName, ...permissionCodes],
          }
        : null,
      login: () => {},
      logout: () => {},
    };
  }, []);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export const useAuth = () => useContext(AuthContext);
