// 将同源、默认端口和末尾斜杠收敛到同一个后端身份，避免会话跨服务复用。
export function getApiBackendIdentity(apiBaseUrl: string): string {
  const origin = typeof window === 'undefined' ? 'http://localhost' : window.location.origin;
  const url = new URL(apiBaseUrl.trim() || '/', origin);
  if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password || url.search || url.hash) {
    throw new Error('API 地址必须是 HTTP(S) 地址，且不能包含凭证、查询参数或片段。');
  }
  return url.href.replace(/\/+$/, '');
}
