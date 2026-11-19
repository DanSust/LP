/**
 * Подбор цвета иконки, наложенной на фотографию.
 *
 * Используется и в `user-photo` (редактирование своих фото),
 * и в `match` (удаление пользователя из галереи).
 */

export type PhotoIconTheme = 'light' | 'dark';

export const PHOTO_ICON_LIGHT = 'photo-icon--light';
export const PHOTO_ICON_DARK = 'photo-icon--dark';

/**
 * Возвращает 'light' для тёмного участка фото и 'dark' для светлого.
 * Анализируется правый верхний угол — именно там обычно лежит иконка удаления.
 */
export function pickIconTheme(img: HTMLImageElement): PhotoIconTheme {
  try {
    const sample = 40; // примерный размер области под иконкой
    const canvas = document.createElement('canvas');
    canvas.width = sample;
    canvas.height = sample;
    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    if (!ctx || !img.naturalWidth || !img.naturalHeight) return 'dark';

    const sx = Math.max(0, img.naturalWidth - sample);
    ctx.drawImage(img, sx, 0, sample, sample, 0, 0, sample, sample);
    const pixels = ctx.getImageData(0, 0, sample, sample).data;

    let luminance = 0;
    let count = 0;
    for (let i = 0; i < pixels.length; i += 4) {
      if (pixels[i + 3] < 128) continue; // прозрачные пиксели не учитываем
      luminance += 0.299 * pixels[i] + 0.587 * pixels[i + 1] + 0.114 * pixels[i + 2];
      count++;
    }
    if (!count) return 'dark';

    return luminance / count < 150 ? 'light' : 'dark';
  } catch {
    // Изображение с другого домена может сделать canvas «грязным» —
    // в этом случае остаётся тёмный вариант с белой тенью.
    return 'dark';
  }
}

/** Класс, который нужно повесить на иконку под выбранную тему. */
export function photoIconClass(theme: PhotoIconTheme): string {
  return theme === 'light' ? PHOTO_ICON_LIGHT : PHOTO_ICON_DARK;
}
