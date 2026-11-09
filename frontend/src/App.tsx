import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, type ReactNode } from 'react';
import { Navigate, Route, Routes, useLocation } from 'react-router';
import { AppShell } from './app/AppShell';
import { sessionExpired, useSession } from './auth/session';
import { OfflineBanner } from './components/OfflineBanner';
import { NotFound } from './components/Page';
import { ToastProvider } from './components/Toast';
import { ActivityPage } from './features/activity/ActivityPage';
import { CompanySettingsPage } from './features/admin/CompanySettingsPage';
import { InvitationsPage } from './features/admin/InvitationsPage';
import { MembersPage } from './features/admin/MembersPage';
import { RequireAdmin } from './features/admin/RequireAdmin';
import { AcceptInvitationPage } from './features/auth/AcceptInvitationPage';
import { CreateWorkspacePage } from './features/auth/CreateWorkspacePage';
import { SessionExpiredPage } from './features/auth/SessionExpiredPage';
import { SignInPage } from './features/auth/SignInPage';
import { GiveProvider } from './features/give/GiveContext';
import { HomePage } from './features/home/HomePage';
import { PeoplePage } from './features/people/PeoplePage';
import { PersonPage } from './features/people/PersonPage';
import { ProfilePage } from './features/profile/ProfilePage';
import { RecognitionPage } from './features/recognition/RecognitionPage';

function RequireAuth({ children }: { children: ReactNode }) {
  const session = useSession();
  const location = useLocation();
  if (!session) {
    // An expired session says so first; a sign-out (or a first visit) goes straight to sign-in
    const to = sessionExpired() ? '/session-expired' : '/sign-in';
    return <Navigate to={to} replace state={{ from: location.pathname + location.search }} />;
  }
  return children;
}

export function App() {
  const session = useSession();
  const queryClient = useQueryClient();

  // Signed out (or the refresh token expired): drop everything cached for that user. Only when a session ends,
  // not whenever there's none: public pages (the invitation lookup) have queries of their own in flight.
  const previous = useRef(session);
  useEffect(() => {
    if (previous.current && !session) queryClient.clear();
    previous.current = session;
  }, [session, queryClient]);

  return (
    <ToastProvider>
      <div className="app-frame">
        <OfflineBanner />
        <div className="app-body">
          <Routes>
            <Route path="/sign-in" element={<SignInPage />} />
            <Route path="/create-workspace" element={<CreateWorkspacePage />} />
            <Route path="/session-expired" element={<SessionExpiredPage />} />
            <Route path="/accept-invitation/:token" element={<AcceptInvitationPage />} />
            <Route
              element={
                <RequireAuth>
                  <GiveProvider>
                    <AppShell />
                  </GiveProvider>
                </RequireAuth>
              }
            >
              <Route index element={<HomePage />} />
              <Route path="activity" element={<ActivityPage />} />
              <Route path="people" element={<PeoplePage />} />
              <Route path="people/:id" element={<PersonPage />} />
              <Route path="me" element={<ProfilePage />} />
              <Route path="recognition/:id" element={<RecognitionPage />} />
              <Route path="admin/members" element={<RequireAdmin><MembersPage /></RequireAdmin>} />
              <Route path="admin/invitations" element={<RequireAdmin><InvitationsPage /></RequireAdmin>} />
              <Route path="admin/settings" element={<RequireAdmin><CompanySettingsPage /></RequireAdmin>} />
              <Route path="*" element={<NotFound />} />
            </Route>
          </Routes>
        </div>
      </div>
    </ToastProvider>
  );
}
