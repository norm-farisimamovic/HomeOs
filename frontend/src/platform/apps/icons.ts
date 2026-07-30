import type { ComponentType } from 'react'
import {
  Archive, Bell, Blocks, Calendar, CheckSquare, GraduationCap, Home, Kanban, Mail, MessageCircle,
  ScrollText, Settings, ShoppingCart, StickyNote, Users, Wallet, Zap,
} from 'lucide-react'

/** Maps a manifest icon name (e.g. "CheckSquare") to its lucide component. Falls back to a generic block. */
const ICONS: Record<string, ComponentType<{ size?: number; className?: string }>> = {
  Home, Users, Mail, ScrollText, Blocks, Settings, CheckSquare, Kanban,
  Wallet, Calendar, Bell, StickyNote, Archive, Zap, ShoppingCart, MessageCircle, GraduationCap,
}

export function appIcon(name: string): ComponentType<{ size?: number; className?: string }> {
  return ICONS[name] ?? Blocks
}
