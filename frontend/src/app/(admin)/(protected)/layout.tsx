import type { ReactNode } from "react";
import { AdminWorkspace } from "@/features/auth/admin-workspace";

export default function ProtectedLayout({ children }: { children: ReactNode }) {
  return <AdminWorkspace>{children}</AdminWorkspace>;
}
