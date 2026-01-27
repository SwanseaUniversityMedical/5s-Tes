import { KeyCloakUser } from "@/types/KeyCloakUser";
import Navbar from "./nav/Navbar";

type HeaderProps = {
  user: KeyCloakUser;
};

export default function Header({
  user
}: HeaderProps) {
  return (
    <header className="sticky py-1 top-0 z-50 border-b bg-background">
        <Navbar user={user}/>
    </header>
  );
}
