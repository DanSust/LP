export function formatDistance(distance ?: number): string {
  if (!distance) return '';
  if (distance < 1) return '< 1 км';
  if (distance < 10) return `${distance.toFixed(1)} км`;
  return `${Math.round(distance)} км`;
}
export function getPeopleWord(count: number): string {
  if (count % 10 === 1 && count % 100 !== 11) {
    return 'человек';
  } else if ([2, 3, 4].includes(count % 10) && ![12, 13, 14].includes(count % 100)) {
    return 'человека';
  } else {
    return 'человек';
  }
}

export function getZodiacSign(birthday: string): string {
  if (!birthday) return '';

  const date = new Date(birthday);
  const day = date.getDate();
  const month = date.getMonth() + 1; // 1-12

  if ((month === 3 && day >= 21) || (month === 4 && day <= 19)) return 'Овен';
  if ((month === 4 && day >= 20) || (month === 5 && day <= 20)) return 'Телец';
  if ((month === 5 && day >= 21) || (month === 6 && day <= 20)) return 'Близнецы';
  if ((month === 6 && day >= 21) || (month === 7 && day <= 22)) return 'Рак';
  if ((month === 7 && day >= 23) || (month === 8 && day <= 22)) return 'Лев';
  if ((month === 8 && day >= 23) || (month === 9 && day <= 22)) return 'Дева';
  if ((month === 9 && day >= 23) || (month === 10 && day <= 22)) return 'Весы';
  if ((month === 10 && day >= 23) || (month === 11 && day <= 21)) return 'Скорпион';
  if ((month === 11 && day >= 22) || (month === 12 && day <= 21)) return 'Стрелец';
  if ((month === 12 && day >= 22) || (month === 1 && day <= 19)) return 'Козерог';
  if ((month === 1 && day >= 20) || (month === 2 && day <= 18)) return 'Водолей';
  if ((month === 2 && day >= 19) || (month === 3 && day <= 20)) return 'Рыбы';

  return '';
}

export function getAgeWord(age: number): string {
  if (age % 100 >= 11 && age % 100 <= 19) {
    return 'лет';
  }
  const lastDigit = age % 10;
  if (lastDigit === 1) {
    return 'год';
  }
  if (lastDigit >= 2 && lastDigit <= 4) {
    return 'года';
  }
  return 'лет';
}
