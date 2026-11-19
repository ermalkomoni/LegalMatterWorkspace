import type { Metadata } from "next";
import { AdminSignIn } from "@/features/auth/admin-sign-in";

export const metadata: Metadata = {
  title: "Sign in | Amanet",
};

export default function LoginPage() {
  return <AdminSignIn />;
}
