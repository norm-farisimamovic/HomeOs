import { createBrowserRouter } from 'react-router-dom'
import { AppShell } from '@/app/AppShell'
import { RequireAuth } from '@/app/components/RequireAuth'
import { DashboardPage } from '@/app/pages/DashboardPage'
import { LoginPage } from '@/app/pages/LoginPage'
import { RegisterPage } from '@/app/pages/RegisterPage'
import { AcceptInvitePage } from '@/app/pages/AcceptInvitePage'
import { ConfirmEmailPage } from '@/app/pages/ConfirmEmailPage'
import { ForgotPasswordPage } from '@/app/pages/ForgotPasswordPage'
import { ResetPasswordPage } from '@/app/pages/ResetPasswordPage'
import { ProfilePage } from '@/app/pages/ProfilePage'
import { SettingsPage } from '@/app/pages/SettingsPage'
import { NotificationsPage } from '@/app/pages/NotificationsPage'
import { AuditPage } from '@/app/pages/AuditPage'
import { AppsPage } from '@/app/pages/AppsPage'
import { TasksPage } from '@/apps/tasks/TasksPage'
import { KanbanPage } from '@/apps/kanban/KanbanPage'
import { FinancePage } from '@/apps/finance/FinancePage'
import { CalendarPage } from '@/apps/calendar/CalendarPage'
import { RemindersPage } from '@/apps/reminders/RemindersPage'
import { NotesPage } from '@/apps/notes/NotesPage'
import { LifeAdminPage } from '@/apps/life/LifeAdminPage'
import { ShoppingPage } from '@/apps/shopping/ShoppingPage'
import { ChatPage } from '@/apps/chat/ChatPage'
import { AssistantPage } from '@/apps/assistant/AssistantPage'
import { ReportsPage } from '@/app/pages/ReportsPage'
import { AutomationsPage } from '@/apps/automations/AutomationsPage'
import { HouseholdPage } from '@/apps/household/HouseholdPage'

export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  { path: '/register', element: <RegisterPage /> },
  { path: '/invite/:token', element: <AcceptInvitePage /> },
  { path: '/confirm-email', element: <ConfirmEmailPage /> },
  { path: '/forgot-password', element: <ForgotPasswordPage /> },
  { path: '/reset-password', element: <ResetPasswordPage /> },
  {
    path: '/',
    element: (
      <RequireAuth>
        <AppShell />
      </RequireAuth>
    ),
    children: [
      { index: true, element: <DashboardPage /> },
      { path: 'tasks', element: <TasksPage /> },
      { path: 'household', element: <HouseholdPage /> },
      { path: 'profile', element: <ProfilePage /> },
      { path: 'settings', element: <SettingsPage /> },
      { path: 'notifications', element: <NotificationsPage /> },
      { path: 'audit', element: <AuditPage /> },
      { path: 'boards', element: <KanbanPage /> },
      { path: 'calendar', element: <CalendarPage /> },
      { path: 'reminders', element: <RemindersPage /> },
      { path: 'notes', element: <NotesPage /> },
      { path: 'finance', element: <FinancePage /> },
      { path: 'life', element: <LifeAdminPage /> },
      { path: 'shopping', element: <ShoppingPage /> },
      { path: 'chat', element: <ChatPage /> },
      { path: 'assistant', element: <AssistantPage /> },
      { path: 'automations', element: <AutomationsPage /> },
      { path: 'reports', element: <ReportsPage /> },
      { path: 'apps', element: <AppsPage /> },
    ],
  },
])
