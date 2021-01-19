using System;
using System.Collections.Generic;
using System.Text;

namespace clowncar.Meta
{
    public static class Defaults
    {
        public static string TemplateText = @"<!doctype html>
<html lang='en'>
<head>
<meta charset='utf-8' name='viewport' content='width=device-width, initial-scale=1.0'>
<title>{{title}}</title>
<link rel='icon' href='data:image/svg+xml,<svg xmlns=%22http://www.w3.org/2000/svg%22 viewBox=%220 0 100 100%22><text y=%22.9em%22 font-size=%2290%22>🤡</text></svg>'>
</head>
<body>
<style>
html {
  background-color:#FFF;
  color:#333;
}
body {
  max-width:70ch;
  padding:2ch;
  margin:auto;
}
pre,blockquote {
  margin-left:4ch;
  margin-right:0;
  background-color:#EEE;
  padding:1ch;
}
pre {
  white-space:pre-wrap;
}
blockquote {
  border-left:1ch solid #AAA;
}
@media (prefers-color-scheme: dark) {
  html {
    filter: invert(100%);
  }
  img:not(.ignore-color-scheme) {
    filter: brightness(50%) invert(100%);
  }
  .ignore-color-scheme {
    filter: invert(100%);
  }
}
</style>
{{body}}
</body>
</html>";
    }
}
