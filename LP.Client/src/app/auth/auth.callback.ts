import { Component, OnInit } from "@angular/core";
import { ActivatedRoute, ParamMap } from "@angular/router";

@Component({
  selector: "call-back",
  template: "<h2></h2>"
})
export class AuthCallbackComponent implements OnInit {
  constructor(private route: ActivatedRoute) { }

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    const code = params.get('code');
    const userId = params.get('userId');

    const channel = new BroadcastChannel('auth-channel');
    channel.postMessage({
      type: 'oauth-done',
      payload: { code: code, userId: userId }
    });
    window.close();
  }

  
}
