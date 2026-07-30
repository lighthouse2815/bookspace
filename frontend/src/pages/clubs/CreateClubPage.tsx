import { ArrowLeft, UsersThree } from '@phosphor-icons/react'
import { Link, useNavigate } from 'react-router-dom'
import { ClubForm } from '../../components/clubs/ClubForm'
import { useToast } from '../../contexts/ToastContext'
import { useCreateClub } from '../../hooks/useSocialProduct'
import { errorMessage } from '../../lib/api'

export function CreateClubPage() {
  const createClub = useCreateClub()
  const navigate = useNavigate()
  const { showToast } = useToast()

  return (
    <div className="container-page section-space max-w-4xl">
      <Link
        to="/clubs"
        className="inline-flex items-center gap-2 text-sm font-semibold text-muted hover:text-heading"
      >
        <ArrowLeft size={17} />
        Tất cả câu lạc bộ
      </Link>

      <div className="mt-8 grid gap-8 lg:grid-cols-[0.72fr_1.28fr] lg:items-start">
        <div>
          <div className="grid h-12 w-12 place-items-center rounded-xl bg-accent-soft text-accent-strong">
            <UsersThree size={25} weight="duotone" />
          </div>
          <p className="eyebrow mt-6">Không gian đọc của bạn</p>
          <h1 className="mt-3 text-3xl font-bold tracking-tight text-heading sm:text-4xl">
            Tạo câu lạc bộ
          </h1>
          <p className="mt-4 leading-7 text-muted">
            Đặt một chủ đề rõ ràng, mời những người cùng gu và chọn cuốn sách đầu tiên để bắt
            đầu.
          </p>
        </div>

        <section className="surface p-5 sm:p-7">
          <ClubForm
            submitLabel="Tạo câu lạc bộ"
            loading={createClub.isPending}
            autoFocus
            onSubmit={async (input) => {
              try {
                const club = await createClub.mutateAsync(input)
                showToast('Đã tạo câu lạc bộ', 'success')
                navigate(`/clubs/${club.id}`, { replace: true })
              } catch (error) {
                showToast(errorMessage(error, 'Không thể tạo câu lạc bộ.'), 'error')
              }
            }}
          />
        </section>
      </div>
    </div>
  )
}
