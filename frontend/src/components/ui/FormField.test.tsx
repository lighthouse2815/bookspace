import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { InputField, SelectField, TextareaField } from './FormField'

describe('shared form fields', () => {
  it('associates input errors with the field while preserving existing descriptions', () => {
    render(
      <>
        <p id="password-rules">Tối thiểu 8 ký tự.</p>
        <InputField
          id="password"
          label="Mật khẩu"
          error="Mật khẩu không hợp lệ."
          aria-describedby="password-rules"
        />
      </>,
    )

    const input = screen.getByRole('textbox', { name: 'Mật khẩu' })
    expect(input).toHaveAttribute('aria-invalid', 'true')
    expect(input).toHaveAttribute(
      'aria-describedby',
      'password-rules password-error',
    )
    expect(input).toHaveAttribute('aria-errormessage', 'password-error')
    expect(screen.getByRole('alert')).toHaveAttribute('id', 'password-error')
  })

  it('associates textarea hints without marking the field invalid', () => {
    render(
      <TextareaField
        id="bio"
        label="Giới thiệu"
        hint="Tối đa 500 ký tự."
      />,
    )

    const textarea = screen.getByRole('textbox', { name: 'Giới thiệu' })
    expect(textarea).toHaveAttribute('aria-invalid', 'false')
    expect(textarea).toHaveAttribute('aria-describedby', 'bio-hint')
    expect(textarea).not.toHaveAttribute('aria-errormessage')
    expect(document.getElementById('bio-hint')).toHaveTextContent('Tối đa 500 ký tự.')
  })

  it('associates select errors with the selected field', () => {
    render(
      <SelectField id="visibility" label="Quyền riêng tư" error="Hãy chọn quyền riêng tư.">
        <option value="">Chọn</option>
      </SelectField>,
    )

    const select = screen.getByRole('combobox', { name: 'Quyền riêng tư' })
    expect(select).toHaveAttribute('aria-describedby', 'visibility-error')
    expect(select).toHaveAttribute('aria-errormessage', 'visibility-error')
    expect(document.getElementById('visibility-error')).toHaveTextContent(
      'Hãy chọn quyền riêng tư.',
    )
  })
})
