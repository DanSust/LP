import { Injectable } from '@angular/core';
import { NativeDateAdapter } from '@angular/material/core';

@Injectable()
export class CustomDateAdapter extends NativeDateAdapter {
  override format(date: Date, displayFormat: Object): string {
    // Предотвращаем сдвиг времени
    const d = new Date(date);
    d.setHours(12); // Устанавливаем 12:00, чтобы избежать перехода через 0:00
    console.log('sfdasdfsafasfd');
    return d.toLocaleDateString('ru-RU'); // Формат русской даты
  }
}
