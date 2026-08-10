import type { InputHTMLAttributes, ReactNode, SelectHTMLAttributes, TextareaHTMLAttributes } from 'react'

interface FieldShellProps {
  label: string
  htmlFor: string
  messageId?: string
  error?: string
  hint?: string
  children: ReactNode
}

function FieldShell({ label, htmlFor, messageId, error, hint, children }: FieldShellProps) {
  return (
    <div className="field">
      <label htmlFor={htmlFor} className="field-label">
        {label}
      </label>
      {children}
      {error ? (
        <p id={messageId} className="field-error" role="alert">
          {error}
        </p>
      ) : hint ? (
        <p id={messageId} className="field-hint">{hint}</p>
      ) : null}
    </div>
  )
}

interface InputFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string
  error?: string
  hint?: string
}

export function InputField({ label, id, error, hint, className = '', ...props }: InputFieldProps) {
  const inputId = id || props.name
  if (!inputId) throw new Error('InputField cần id hoặc name')
  const messageId = error ? `${inputId}-error` : hint ? `${inputId}-hint` : undefined
  const describedBy = [props['aria-describedby'], messageId].filter(Boolean).join(' ') || undefined
  return (
    <FieldShell
      label={label}
      htmlFor={inputId}
      messageId={messageId}
      error={error}
      hint={hint}
    >
      <input
        {...props}
        id={inputId}
        className={`input ${error ? 'input-error' : ''} ${className}`}
        aria-invalid={Boolean(error)}
        aria-describedby={describedBy}
        aria-errormessage={error ? messageId : undefined}
      />
    </FieldShell>
  )
}

interface TextareaFieldProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  label: string
  error?: string
  hint?: string
}

export function TextareaField({
  label,
  id,
  error,
  hint,
  className = '',
  ...props
}: TextareaFieldProps) {
  const inputId = id || props.name
  if (!inputId) throw new Error('TextareaField cần id hoặc name')
  const messageId = error ? `${inputId}-error` : hint ? `${inputId}-hint` : undefined
  const describedBy = [props['aria-describedby'], messageId].filter(Boolean).join(' ') || undefined
  return (
    <FieldShell
      label={label}
      htmlFor={inputId}
      messageId={messageId}
      error={error}
      hint={hint}
    >
      <textarea
        {...props}
        id={inputId}
        className={`input min-h-28 resize-y ${error ? 'input-error' : ''} ${className}`}
        aria-invalid={Boolean(error)}
        aria-describedby={describedBy}
        aria-errormessage={error ? messageId : undefined}
      />
    </FieldShell>
  )
}

interface SelectFieldProps extends SelectHTMLAttributes<HTMLSelectElement> {
  label: string
  error?: string
  hint?: string
}

export function SelectField({
  label,
  id,
  error,
  hint,
  className = '',
  children,
  ...props
}: SelectFieldProps) {
  const inputId = id || props.name
  if (!inputId) throw new Error('SelectField cần id hoặc name')
  const messageId = error ? `${inputId}-error` : hint ? `${inputId}-hint` : undefined
  const describedBy = [props['aria-describedby'], messageId].filter(Boolean).join(' ') || undefined
  return (
    <FieldShell
      label={label}
      htmlFor={inputId}
      messageId={messageId}
      error={error}
      hint={hint}
    >
      <select
        {...props}
        id={inputId}
        className={`input ${error ? 'input-error' : ''} ${className}`}
        aria-invalid={Boolean(error)}
        aria-describedby={describedBy}
        aria-errormessage={error ? messageId : undefined}
      >
        {children}
      </select>
    </FieldShell>
  )
}
