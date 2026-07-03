import { NavLink } from 'react-router-dom';
import { selectIsRecordingLocked, useRecordSessionStore } from '../features/record/store/recordSessionStore';
import { desktopNavItems } from './navigation/navItems';
import { RecordButton } from './RecordButton';

export function SideNav() {
  const isRecordingLocked = useRecordSessionStore(selectIsRecordingLocked);

  function navItemClass(isActive: boolean) {
    return [
      'side-nav__item',
      isActive ? 'side-nav__item--active' : undefined,
      isRecordingLocked ? 'side-nav__item--disabled' : undefined,
    ]
      .filter(Boolean)
      .join(' ');
  }

  return (
    <nav className="side-nav" aria-label="Main navigation">
      <div className="side-nav__record">
        <RecordButton label="Record" disabled={isRecordingLocked} />
      </div>

      <p className="side-nav__section-label">Menu</p>

      {desktopNavItems.map((item) => (
        <NavLink
          key={item.id}
          to={item.to}
          end={item.end}
          aria-disabled={isRecordingLocked}
          tabIndex={isRecordingLocked ? -1 : undefined}
          onClick={(event) => {
            if (isRecordingLocked) event.preventDefault();
          }}
          className={({ isActive }) => navItemClass(isActive)}
        >
          <span className="side-nav__icon" aria-hidden="true">
            {item.icon}
          </span>
          <span className="side-nav__text">{item.label}</span>
        </NavLink>
      ))}
    </nav>
  );
}
