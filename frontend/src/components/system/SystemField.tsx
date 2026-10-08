export interface SystemFieldProps {
  label: string;
  error?: string;
  prefixIcon?: string;
  children: React.ReactNode;
}

export default function SystemField({
  label,
  error,
  prefixIcon,
  children,
}: SystemFieldProps): React.JSX.Element {
  const renderLabel = (text: string) => {
    const trimmed = text.trim();
    if (trimmed.endsWith('*')) {
      const mainText = trimmed.slice(0, -1).trim();
      return (
        <>
          {mainText}
          <span style={{ color: '#dc2626', marginLeft: '2px', fontWeight: 'bold' }}>*</span>
        </>
      );
    }
    return text;
  };

  return (
    <div className="system-field">
      <div className={`system-field__outline${error ? ' system-field__outline--error' : ''}`}>
        {prefixIcon && (
          <span className="system-field__prefix">
            <span className="material-symbols-outlined">{prefixIcon}</span>
          </span>
        )}
        <div className="system-field__control-wrap">
          <span className="system-field__label">{renderLabel(label)}</span>
          {children}
        </div>
      </div>
      {error && <span className="system-field__error">{error}</span>}
    </div>
  );
}
