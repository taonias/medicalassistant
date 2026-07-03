import { NavLink } from 'react-router-dom';
import { selectIsRecordingLocked, useRecordSessionStore } from '../features/record/store/recordSessionStore';
import { mainNavItems } from './navigation/navItems';
import { RecordButton } from './RecordButton';

export function FooterNav() {
  const isRecordingLocked = useRecordSessionStore(selectIsRecordingLocked);
  const sideItems = mainNavItems.filter((item) => !item.isPrimary);
  const leftItems = sideItems.slice(0, 2);
  const rightItems = sideItems.slice(2);

  function navItemClass(isActive: boolean) {
    return [
      'footer-nav__item',
      isActive ? 'footer-nav__item--active' : undefined,
      isRecordingLocked ? 'footer-nav__item--disabled' : undefined,
    ]
      .filter(Boolean)
      .join(' ');
  }

  return (
    <nav className="footer-nav" aria-label="Main navigation">
      <div className="footer-nav__inner">
        {leftItems.map((item) => (
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
            <span className="footer-nav__icon" aria-hidden="true">
              {item.icon}
            </span>
            <span className="footer-nav__label">{item.label}</span>
          </NavLink>
        ))}

        <RecordButton className="footer-nav__record" disabled={isRecordingLocked} />

        {rightItems.map((item) => (
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
            <span className="footer-nav__icon" aria-hidden="true">
              {item.icon}
            </span>
            <span className="footer-nav__label">{item.label}</span>
          </NavLink>
        ))}
      </div>
    </nav>
  );
}
