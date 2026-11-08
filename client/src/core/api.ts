// API client and shared types. Mirrors the contracts in server/src/SmsService.DTO.

export const API_URL: string = import.meta.env.VITE_API_URL ?? 'http://localhost:5000'

export type MessageStatus = 'Pending' | 'Sent' | 'Delivered' | 'Failed'
export const MESSAGE_STATUSES: MessageStatus[] = ['Pending', 'Sent', 'Delivered', 'Failed']

export interface Phone {
  phoneId: number
  number: string
}

export interface Employee {
  id: number
  firstName: string
  lastName: string
  address: string
  phones: Phone[]
}

export interface Message {
  id: string
  status: MessageStatus
  body: string
  createdAt: string
  updatedAt: string
  failureReason: string | null
  employee: { firstName: string; lastName: string; address: string }
  phoneNumber: string
}

export interface CreateMessageResponse {
  id: string
  status: MessageStatus
}

/** Error carrying the ProblemDetails "code" and "detail" from the API. */
export class ApiError extends Error {
  constructor(public code: string | undefined, message: string) {
    super(message)
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_URL}${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...(init?.headers ?? {}) },
  })
  if (!res.ok) {
    let code: string | undefined
    let detail = `HTTP ${res.status}`
    try {
      const problem = await res.json()
      code = problem.code
      detail = problem.detail ?? problem.title ?? detail
    } catch {
      /* body was not JSON */
    }
    throw new ApiError(code, detail)
  }
  return res.json() as Promise<T>
}

export const api = {
  getEmployees: () => request<Employee[]>('/employees'),

  getMessages: (status?: MessageStatus) =>
    request<Message[]>(status ? `/messages?status=${encodeURIComponent(status)}` : '/messages'),

  sendMessage: (phoneId: number, body: string, idempotencyKey: string) =>
    request<CreateMessageResponse>('/messages', {
      method: 'POST',
      headers: { 'Idempotency-Key': idempotencyKey },
      body: JSON.stringify({ phoneId, body }),
    }),
}
