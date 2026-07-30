export const themeOptions = [
  {
    id: 'light',
    name: 'Sáng',
    description: 'Nền sáng, tương phản rõ để đọc ban ngày',
    colorScheme: 'light',
  },
  {
    id: 'paper',
    name: 'Giấy ngà',
    description: 'Ấm như trang sách cũ, dịu hơn nền trắng',
    colorScheme: 'light',
  },
  {
    id: 'dark',
    name: 'Mực đêm',
    description: 'Xanh than với điểm nhấn đồng cổ',
    colorScheme: 'dark',
  },
  {
    id: 'forest',
    name: 'Rừng đêm',
    description: 'Xanh rêu trầm, thoải mái khi đọc lâu',
    colorScheme: 'dark',
  },
  {
    id: 'plum',
    name: 'Mận khói',
    description: 'Tím mận tiết chế, có chiều sâu',
    colorScheme: 'dark',
  },
] as const

export type Theme = (typeof themeOptions)[number]['id']

export function isDarkTheme(theme: Theme) {
  return themeOptions.find((option) => option.id === theme)?.colorScheme === 'dark'
}
