import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { API_BASE_URL } from './../app.config';


@Component({
  selector: 'app-back-collage',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './background-collage.component.html',
  styleUrls: ['./background-collage.component.scss']
})
export class BottomNavComponent {  

  constructor(@Inject(API_BASE_URL) private base: string) { }

  
}
