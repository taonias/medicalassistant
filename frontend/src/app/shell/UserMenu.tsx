import { useEffect, useRef, useState } from "react";
import { useAuthStore } from "../../features/auth";
import { selectIsRecordingLocked, useRecordSessionStore } from "../../features/record";
import { formatPatientName } from "../../shared/utils/format";
import { UserIcon } from "./navigation/NavIcons";

interface Props {
  variant?: "default" | "sidebar";
}

export function UserMenu({ variant = "default" }: Props) {
  const user = useAuthStore((state) => state.user);
  const roles = useAuthStore((state) => state.roles);
  const isRecordingLocked = useRecordSessionStore(selectIsRecordingLocked);
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (isRecordingLocked) setOpen(false);
  }, [isRecordingLocked]);

  useEffect(() => {
    if (!open) return;

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") setOpen(false);
    }

    function onPointerDown(event: MouseEvent) {
      if (!rootRef.current?.contains(event.target as Node)) {
        setOpen(false);
      }
    }

    window.addEventListener("keydown", onKeyDown);
    window.addEventListener("mousedown", onPointerDown);
    return () => {
      window.removeEventListener("keydown", onKeyDown);
      window.removeEventListener("mousedown", onPointerDown);
    };
  }, [open]);

  const displayName = user
    ? formatPatientName(user.firstName, user.lastName)
    : "Signed in user";

  const isSidebar = variant === "sidebar";

  return (
    <div
      className={`user-menu${isSidebar ? " user-menu--sidebar" : ""}`}
      ref={rootRef}
    >
      <button
        type="button"
        className={isSidebar ? "user-menu__trigger" : "icon-button"}
        aria-label={isSidebar ? undefined : "View account information"}
        aria-expanded={open}
        aria-haspopup="dialog"
        disabled={isRecordingLocked}
        onClick={() => setOpen((value) => !value)}
      >
        <UserIcon />
        {isSidebar ? (
          <span className="user-menu__trigger-text">
            <span className="user-menu__trigger-name">{displayName}</span>
            <span className="user-menu__trigger-role">
              {roles.length > 0 ? roles.join(", ") : "Doctor"}
            </span>
          </span>
        ) : null}
      </button>

      {open ? (
        <div
          className="user-menu__panel"
          role="dialog"
          aria-label="Account information"
        >
          <div className="user-menu__header">
            <div className="user-menu__avatar" aria-hidden="true">
              <UserIcon />
            </div>
            <div>
              <strong>{displayName}</strong>
              <p className="muted user-menu__subtitle">
                {roles.length > 0 ? roles.join(", ") : "Doctor"}
              </p>
            </div>
          </div>

          <dl className="user-menu__details">
            <div>
              <dt>Username</dt>
              <dd>{user?.userName || "—"}</dd>
            </div>
            <div>
              <dt>Email</dt>
              <dd>{user?.email || "—"}</dd>
            </div>
          </dl>
        </div>
      ) : null}
    </div>
  );
}
