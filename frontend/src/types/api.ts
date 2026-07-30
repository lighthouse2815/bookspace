export interface ApiEnvelope<T> {
  success: boolean
  message: string
  data: T
  code?: string | null
  timestamp: string
}

export interface PageResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export interface PageQuery {
  page?: number
  pageSize?: number
}
