import { Component, ViewEncapsulation, EventEmitter, Inject, Output, ElementRef, ViewChild } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { NgxFileDropEntry, FileSystemFileEntry, NgxFileDropModule } from 'ngx-file-drop';
import { API_BASE_URL } from './../app.config'; //'../../main';

@Component({
  selector: 'file-upload',
  standalone: true,
  imports: [NgxFileDropModule],
  styleUrls: ['./file-upload.component.css'],
  templateUrl: './file-upload.component.html',
  encapsulation: ViewEncapsulation.None
})
export class FileUploadComponent {
  public files: NgxFileDropEntry[] = [];
  @Output() fileUploaded = new EventEmitter<any>();
  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;

  constructor(
    private http: HttpClient,
    @Inject(API_BASE_URL) private base: string) {
    //console.log(base);    
  }

  dropped(files: NgxFileDropEntry[]) {
    this.files = files;
    for (const droppedFile of files) {
      if (droppedFile.fileEntry.isFile) {
        const fileEntry = droppedFile.fileEntry as FileSystemFileEntry;
        fileEntry.file((file: File) => {
          console.log(droppedFile.relativePath, file);
          // Upload file here
          this.uploadFile(file);
        });
      }
    }
  }

  uploadFile(file: File): void {
    const formData = new FormData();
    formData.append('file', file, file.name);

    const upload$ = this.http.post<{ userId: string; id: string }>(this.base + "/Photos/add", formData, { withCredentials: true });

    upload$.subscribe(result => {
      this.fileUploaded.emit(result);
      //this.items = [...this.items, result];
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      const file = input.files[0];
      this.uploadFile(file);
      // Сбрасываем значение, чтобы можно было выбрать тот же файл снова
      input.value = '';
    }
  }

  openFileDialog(): void {
    this.fileInput.nativeElement.click();
  }
}
