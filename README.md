# Director Comercial IA

Aplicación web interna en Blazor Server + SQLite para leer oportunidades comerciales desde Asana.

## Requisitos

- Visual Studio 2022 actualizado o .NET SDK 8 instalado.
- Token de Asana.

## Configuración

Edita `appsettings.json`:

```json
"Asana": {
  "Token": "PON_AQUI_TU_TOKEN_DE_ASANA"
}
```

No compartas este token ni lo subas a repositorios públicos.

## Estilos (Tailwind CSS)

El proyecto usa **Tailwind CSS v4** (mismo enfoque que CommercialMail). Los estilos se compilan de `Styles/app.css` a `wwwroot/css/site.css`.

```bash
npm install
npm run build:css
```

Durante el desarrollo, puedes dejar Tailwind en modo watch en otra terminal:

```bash
npm run watch:css
```

`dotnet build` también ejecuta `npm run build:css` automáticamente si existe `package.json`.

## Ejecutar desde terminal

```bash
npm install
npm run build:css
dotnet restore
dotnet run
```

Después abre la URL que indique la consola, normalmente:

```text
https://localhost:5001
```

## Ejecutar desde Visual Studio

1. Abre `DirectorComercialIA.csproj`.
2. Sustituye el token en `appsettings.json`.
3. Pulsa F5.
4. Entra en el Dashboard.
5. Pulsa `Actualizar desde Asana`.

## Qué hace ahora

- Lee secciones comerciales de General International.
- Guarda las tareas en SQLite: `director_comercial.db`.
- Muestra oportunidades vencidas, sin fecha, sin responsable y sin actividad.
- Permite filtrar por sección, riesgo y nombre.

## Qué no hace todavía

- No modifica Asana.
- No envía emails.
- No usa ChatGPT API todavía.
- No tiene login.
