## Default Template

This is the default template that is used if no template is provided.

    <!doctype html>
    <html lang='en'>
    <head>
    <meta charset='utf-8' name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{{title}}</title>
    <link rel='icon' href='data:image/svg+xml,<svg xmlns=%22http://www.w3.org/2000/svg%22 viewBox=%220 0 100 100%22><text y=%22.9em%22 font-size=%2290%22>🤡</text></svg>'>
    </head>
    <body>
    <style>
    body {
        max-width:70ch;
        padding:2ch;
        margin:auto;
        color:#333;
        font-size:1.2em;
        background-color:#FFF;
    }
    pre {
        white-space:pre-wrap;
        margin-left:4ch;
        background-color:#EEE;
        padding:1ch;
        border-radius:1ch;
    }
    </style>
    {{body}}
    </body>
    </html>