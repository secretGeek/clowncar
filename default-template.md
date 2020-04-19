## Default Template

This is the default template that is used if no template is provided.

    <!doctype html>
    <html lang='en'>
    <head>
    <meta charset='utf-8' name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{{title}}</title>
    </head>
    <body>
    <style>
    body {
        max-width:70ch;
        padding:2ch;
        margin:auto;
        color:#333;
        font-size:1.2em;
        background-color:#F2F2F2;
    }
    pre {
        white-space:pre-wrap;
        margin-left:4ch;
        background-color:#FFF;
        padding:1ch;
        border-radius:4px;
    }
    </style>
    {{body}}
    </body>
    <script>
    </script>
    </html>