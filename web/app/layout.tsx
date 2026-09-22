import './style.css';
export const metadata = { title: 'ChordApp', description: 'Reconhecimento de acordes' };
export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="pt-BR"><body>{children}</body></html>;
}
