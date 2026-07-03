interface Props {
  variant?: 'horizontal' | 'vertical';
  className?: string;
}

export function AppBrand({ variant = 'vertical', className }: Props) {
  const rootClass = [
    'app-brand',
    variant === 'horizontal' ? 'app-brand--horizontal' : 'app-brand--vertical',
    className,
  ]
    .filter(Boolean)
    .join(' ');

  const src = variant === 'horizontal' ? '/logo-horizontal.png' : '/logo-vertical.png';

  return (
    <div className={rootClass}>
      <img src={src} alt="Medical Assistant" className="app-brand__logo" />
    </div>
  );
}
