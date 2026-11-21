/** Hands the browser a file to save, as a download. */
export function saveFile(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  document.body.append(a);
  a.click();
  a.remove();
  // After the click has started the download
  setTimeout(() => URL.revokeObjectURL(url), 0);
}

/**
 * One CSV cell. A spreadsheet app runs a cell that starts with = + - @ (or a tab or carriage return) as a formula,
 * so those get a leading apostrophe and stay text (CSV injection). Then the usual quoting.
 */
export function csvCell(value: string | number): string {
  let text = String(value);
  if (/^[=+\-@\t\r]/.test(text)) text = `'${text}`;
  return /[",\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}

/** A CSV file that Excel opens as UTF-8 (the byte-order mark) with its rows on separate lines (CRLF). */
export function toCsv(rows: (string | number)[][]): Blob {
  const text = rows.map((r) => r.map(csvCell).join(',')).join('\r\n') + '\r\n';
  return new Blob(['﻿' + text], { type: 'text/csv;charset=utf-8' });
}
