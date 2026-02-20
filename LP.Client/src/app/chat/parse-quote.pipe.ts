import { Pipe, PipeTransform } from '@angular/core';

const QUOTE_DELIMITER = '[[[QUOTE:';
const QUOTE_END = ']]]';

export interface ParsedMessage {
  hasQuote: boolean;
  quoteText?: string;
  mainText: string;
}

@Pipe({
  name: 'parseQuote',
  standalone: true
})
export class ParseQuotePipe implements PipeTransform {
  transform(text: string): ParsedMessage {
    const startIndex = text.indexOf(QUOTE_DELIMITER);
    const endIndex = text.indexOf(QUOTE_END);

    if (startIndex === -1 || endIndex === -1) {
      return { hasQuote: false, mainText: text };
    }

    const quoteText = text.substring(startIndex + QUOTE_DELIMITER.length, endIndex);
    const mainText = text.substring(endIndex + QUOTE_END.length).trim();

    return {
      hasQuote: true,
      quoteText,
      mainText
    };
  }
}
