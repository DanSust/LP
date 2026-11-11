import { Chat } from '../services/ChatService';

/** Срок жизни чата без единого сообщения. */
export const EMPTY_CHAT_TTL_MS = 7 * 24 * 60 * 60 * 1000;

export interface ChatExpiry {
  days: number;
  hours: number;
  text: string;
}

/**
 * Возвращает остаток до удаления для нового чата без сообщений.
 * Для чатов с сообщениями и для истёкшего срока возвращает null.
 */
export function getEmptyChatExpiry(chat: Chat, now: number): ChatExpiry | null {
  if (hasChatMessages(chat) || !chat.time) {
    return null;
  }

  const createdAt = new Date(chat.time).getTime();
  if (isNaN(createdAt)) {
    return null;
  }

  const remaining = createdAt + EMPTY_CHAT_TTL_MS - now;
  if (remaining <= 0) {
    return null;
  }

  const totalHours = Math.floor(remaining / (60 * 60 * 1000));
  const days = Math.floor(totalHours / 24);
  const hours = totalHours % 24;

  return {
    days,
    hours,
    text: `${days} д ${hours} ч`
  };
}

export function hasChatMessages(chat: Chat): boolean {
  if (!chat.lastMessageTime) {
    return false;
  }

  const lastMessageTime = new Date(chat.lastMessageTime);
  return !isNaN(lastMessageTime.getTime()) && lastMessageTime.getFullYear() > 1;
}
