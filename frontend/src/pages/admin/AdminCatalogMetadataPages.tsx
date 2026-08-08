import {
  IdentificationCard,
  MagnifyingGlass,
  NotePencil,
  Plus,
  Tag,
  Trash,
  X,
} from '@phosphor-icons/react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { AdminNav } from '../../components/admin/AdminNav'
import { Button } from '../../components/ui/Button'
import { InputField, TextareaField } from '../../components/ui/FormField'
import { Pagination } from '../../components/ui/Pagination'
import { EmptyState, ErrorState, LoadingRows } from '../../components/ui/States'
import { useToast } from '../../contexts/ToastContext'
import { errorMessage } from '../../lib/api'
import {
  adminService,
  type AuthorAdminInput,
  type CategoryAdminInput,
} from '../../services/admin.service'
import type { Author, Category } from '../../types/domain'

type MetadataKind = 'authors' | 'categories'
type MetadataItem = Author | Category

interface MetadataForm {
  name: string
  detail: string
  avatarUrl: string
}

const emptyForm: MetadataForm = { name: '', detail: '', avatarUrl: '' }
const pageSize = 20

const pageConfig = {
  authors: {
    eyebrow: 'Dữ liệu catalog',
    title: 'Quản lý tác giả',
    description: 'Chuẩn hóa hồ sơ tác giả dùng trong catalog và luồng import sách.',
    singular: 'tác giả',
    detailLabel: 'Tiểu sử',
    detailPlaceholder: 'Giới thiệu ngắn về tác giả',
    emptyTitle: 'Chưa có tác giả phù hợp',
    emptyDescription: 'Thử từ khóa khác hoặc tạo hồ sơ tác giả mới.',
    icon: IdentificationCard,
  },
  categories: {
    eyebrow: 'Dữ liệu catalog',
    title: 'Quản lý thể loại',
    description: 'Duy trì hệ phân loại nhất quán cho catalog và khám phá sách.',
    singular: 'thể loại',
    detailLabel: 'Mô tả',
    detailPlaceholder: 'Mô tả phạm vi của thể loại',
    emptyTitle: 'Chưa có thể loại phù hợp',
    emptyDescription: 'Thử từ khóa khác hoặc tạo thể loại mới.',
    icon: Tag,
  },
} as const

function itemDetail(item: MetadataItem, kind: MetadataKind) {
  return kind === 'authors'
    ? (item as Author).biography
    : (item as Category).description
}

function CatalogMetadataPage({ kind }: { kind: MetadataKind }) {
  const config = pageConfig[kind]
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const [searchParams, setSearchParams] = useSearchParams()
  const search = searchParams.get('search')?.trim() ?? ''
  const parsedPage = Number(searchParams.get('page'))
  const page = Number.isInteger(parsedPage) && parsedPage > 0 ? parsedPage : 1
  const [searchInput, setSearchInput] = useState(search)
  const [showForm, setShowForm] = useState(false)
  const [editing, setEditing] = useState<MetadataItem | null>(null)
  const [form, setForm] = useState<MetadataForm>(emptyForm)

  useEffect(() => setSearchInput(search), [search])

  const metadata = useQuery({
    queryKey: ['admin', kind, { search, page, pageSize }],
    queryFn: () =>
      kind === 'authors'
        ? adminService.authors({ search: search || undefined, page, pageSize })
        : adminService.categories({ search: search || undefined, page, pageSize }),
  })

  const closeForm = () => {
    setShowForm(false)
    setEditing(null)
    setForm(emptyForm)
  }

  const refreshCatalog = () => {
    void queryClient.invalidateQueries({ queryKey: ['admin', kind] })
    void queryClient.invalidateQueries({ queryKey: ['catalog'] })
  }

  const save = useMutation({
    mutationFn: async (values: MetadataForm) => {
      if (kind === 'authors') {
        const input: AuthorAdminInput = {
          name: values.name,
          biography: values.detail || undefined,
          avatarUrl: values.avatarUrl || undefined,
        }
        return editing
          ? adminService.updateAuthor(editing.id, input)
          : adminService.createAuthor(input)
      }

      const input: CategoryAdminInput = {
        name: values.name,
        description: values.detail || undefined,
      }
      return editing
        ? adminService.updateCategory(editing.id, input)
        : adminService.createCategory(input)
    },
    onSuccess: () => {
      refreshCatalog()
      showToast(
        editing
          ? `Đã cập nhật ${config.singular}`
          : `Đã thêm ${config.singular} vào catalog`,
        'success',
      )
      closeForm()
    },
    onError: (error) => showToast(errorMessage(error), 'error'),
  })

  const remove = useMutation({
    mutationFn: (item: MetadataItem) =>
      kind === 'authors'
        ? adminService.deleteAuthor(item.id)
        : adminService.deleteCategory(item.id),
    onSuccess: (_, item) => {
      refreshCatalog()
      showToast(`Đã xóa ${config.singular} “${item.name}”`, 'success')
    },
    onError: (error) => showToast(errorMessage(error), 'error'),
  })

  const openCreateForm = () => {
    setEditing(null)
    setForm(emptyForm)
    setShowForm(true)
  }

  const openEditForm = (item: MetadataItem) => {
    setEditing(item)
    setForm({
      name: item.name,
      detail: itemDetail(item, kind) ?? '',
      avatarUrl: kind === 'authors' ? (item as Author).avatarUrl ?? '' : '',
    })
    setShowForm(true)
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  const submitForm = (event: FormEvent) => {
    event.preventDefault()
    const name = form.name.trim()
    if (!name) {
      showToast(`Tên ${config.singular} là bắt buộc`, 'error')
      return
    }
    save.mutate({
      name,
      detail: form.detail.trim(),
      avatarUrl: form.avatarUrl.trim(),
    })
  }

  const submitSearch = (event: FormEvent) => {
    event.preventDefault()
    const next = new URLSearchParams(searchParams)
    const keyword = searchInput.trim()
    if (keyword) next.set('search', keyword)
    else next.delete('search')
    next.delete('page')
    setSearchParams(next)
  }

  const changePage = (nextPage: number) => {
    const next = new URLSearchParams(searchParams)
    if (nextPage > 1) next.set('page', String(nextPage))
    else next.delete('page')
    setSearchParams(next)
  }

  const items = metadata.data?.items ?? []

  return (
    <div className="container-page section-space">
      <p className="eyebrow">{config.eyebrow}</p>
      <div className="mt-4 flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="page-title">{config.title}</h1>
          <p className="mt-3 max-w-2xl text-muted">{config.description}</p>
        </div>
        <Button
          icon={showForm ? <X size={18} /> : <Plus size={18} />}
          onClick={() => (showForm ? closeForm() : openCreateForm())}
        >
          {showForm ? 'Đóng biểu mẫu' : `Thêm ${config.singular}`}
        </Button>
      </div>

      <AdminNav />

      {showForm ? (
        <form className="mb-8 surface p-5 sm:p-7" onSubmit={submitForm}>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <p className="eyebrow">{editing ? 'Chỉnh sửa hồ sơ' : 'Metadata mới'}</p>
              <h2 className="mt-2 text-xl font-bold text-heading">
                {editing ? editing.name : `Thêm ${config.singular}`}
              </h2>
            </div>
            {editing ? (
              <Button type="button" variant="ghost" size="sm" onClick={openCreateForm}>
                Chuyển sang tạo mới
              </Button>
            ) : null}
          </div>
          <div className="mt-6 grid gap-5 md:grid-cols-2">
            <InputField
              label={`Tên ${config.singular}`}
              name={`${kind}-name`}
              value={form.name}
              maxLength={kind === 'authors' ? 200 : 100}
              onChange={(event) => setForm({ ...form, name: event.target.value })}
              required
            />
            {kind === 'authors' ? (
              <InputField
                label="URL ảnh đại diện"
                name="author-avatar-url"
                type="url"
                value={form.avatarUrl}
                maxLength={1000}
                placeholder="https://..."
                onChange={(event) => setForm({ ...form, avatarUrl: event.target.value })}
              />
            ) : (
              <div className="hidden md:block" aria-hidden />
            )}
            <div className="md:col-span-2">
              <TextareaField
                label={config.detailLabel}
                name={`${kind}-detail`}
                value={form.detail}
                maxLength={kind === 'authors' ? 2000 : 500}
                placeholder={config.detailPlaceholder}
                onChange={(event) => setForm({ ...form, detail: event.target.value })}
              />
            </div>
          </div>
          <div className="mt-6 flex justify-end">
            <Button type="submit" loading={save.isPending}>
              {editing ? 'Lưu thay đổi' : `Thêm ${config.singular}`}
            </Button>
          </div>
        </form>
      ) : null}

      <form className="mb-6 flex max-w-2xl gap-2" onSubmit={submitSearch} role="search">
        <label htmlFor={`${kind}-search`} className="sr-only">
          Tìm kiếm {config.singular}
        </label>
        <input
          id={`${kind}-search`}
          className="input min-w-0 flex-1"
          value={searchInput}
          maxLength={200}
          placeholder={`Tìm theo tên hoặc ${config.detailLabel.toLowerCase()}...`}
          onChange={(event) => setSearchInput(event.target.value)}
        />
        <Button type="submit" variant="secondary" icon={<MagnifyingGlass size={18} />}>
          Tìm kiếm
        </Button>
      </form>

      {metadata.isLoading ? (
        <LoadingRows count={6} />
      ) : metadata.isError ? (
        <ErrorState
          message={`Không thể tải danh sách ${config.singular}.`}
          retry={() => void metadata.refetch()}
        />
      ) : items.length === 0 ? (
        <EmptyState
          title={config.emptyTitle}
          description={config.emptyDescription}
          icon={config.icon}
          action={showForm ? undefined : (
            <Button size="sm" icon={<Plus size={16} />} onClick={openCreateForm}>
              Thêm {config.singular}
            </Button>
          )}
        />
      ) : (
        <>
          <div className="overflow-x-auto rounded-2xl border border-border">
            <table className="w-full min-w-[720px] border-collapse text-left text-sm">
              <thead className="bg-surface-muted text-xs uppercase tracking-wider text-muted">
                <tr>
                  <th className="px-4 py-3 font-semibold">{config.singular}</th>
                  <th className="px-4 py-3 font-semibold">{config.detailLabel}</th>
                  <th className="px-4 py-3 font-semibold">Số sách</th>
                  <th className="px-4 py-3 text-right font-semibold">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border bg-surface">
                {items.map((item) => {
                  const attachedBooks = item.bookCount ?? 0
                  const cannotDelete = attachedBooks > 0
                  return (
                    <tr key={item.id}>
                      <td className="px-4 py-4">
                        <div className="flex items-center gap-3">
                          {kind === 'authors' && (item as Author).avatarUrl ? (
                            <img
                              src={(item as Author).avatarUrl}
                              alt=""
                              className="h-10 w-10 rounded-full border border-border object-cover"
                            />
                          ) : (
                            <span className="grid h-10 w-10 place-items-center rounded-full bg-accent-soft text-accent-strong">
                              <config.icon size={19} weight="duotone" aria-hidden />
                            </span>
                          )}
                          <span className="font-semibold text-heading">{item.name}</span>
                        </div>
                      </td>
                      <td className="max-w-md px-4 py-4 text-muted">
                        <p className="line-clamp-2">
                          {itemDetail(item, kind) || 'Chưa có nội dung'}
                        </p>
                      </td>
                      <td className="px-4 py-4">
                        <span className="rounded-full bg-surface-muted px-2.5 py-1 font-semibold text-body">
                          {attachedBooks}
                        </span>
                      </td>
                      <td className="px-4 py-4">
                        <div className="flex justify-end gap-1">
                          <button
                            type="button"
                            className="icon-button"
                            onClick={() => openEditForm(item)}
                            aria-label={`Chỉnh sửa ${item.name}`}
                          >
                            <NotePencil size={18} />
                          </button>
                          <button
                            type="button"
                            className="icon-button text-red-600"
                            disabled={cannotDelete || remove.isPending}
                            title={
                              cannotDelete
                                ? `Không thể xóa vì đang gắn với ${attachedBooks} sách`
                                : undefined
                            }
                            onClick={() => {
                              if (window.confirm(`Xóa ${config.singular} “${item.name}”?`)) {
                                remove.mutate(item)
                              }
                            }}
                            aria-label={`Xóa ${item.name}`}
                          >
                            <Trash size={18} />
                          </button>
                        </div>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
          <Pagination
            className="mt-6"
            page={metadata.data?.page ?? page}
            totalPages={metadata.data?.totalPages ?? 0}
            disabled={metadata.isFetching}
            onPageChange={changePage}
          />
        </>
      )}
    </div>
  )
}

export function AdminAuthorsPage() {
  return <CatalogMetadataPage kind="authors" />
}

export function AdminCategoriesPage() {
  return <CatalogMetadataPage kind="categories" />
}
