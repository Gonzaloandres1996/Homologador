# Manual de Instalación - Homologador de Packings Externos (DDC)

Este documento detalla los pasos para instalar y ejecutar la aplicación en cualquier equipo con sistema operativo Windows.

---

## 1. Requisitos del Sistema

* **Sistema Operativo:** Windows 10 o Windows 11 (64 bits).
* **Conectividad:** Conexión a Internet (necesaria para la detección y descarga de actualizaciones automáticas).
* **Runtime de .NET:** [.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0) de Microsoft.
* **Base de Datos:** Acceso a la base de datos SQL Server (`DataHomologator_Migracion`) local (`.\SQLEXPRESS`) o remota en red.

---

## 2. Pasos de Instalación

### Paso 1: Descargar el Instalador
Descargue el archivo de instalación **`Homologador-win-Setup.exe`** desde la sección de [Releases en GitHub](https://github.com/Gonzaloandres1996/Homologador/releases) o desde el medio de distribución proporcionado.

### Paso 2: Ejecutar el Instalador
1. Haga doble clic sobre **`Homologador-win-Setup.exe`**.
2. **Aviso de SmartScreen (si aparece):**
   * Al tratarse de un ejecutable nuevo, Windows puede mostrar la advertencia *"Windows protegió su PC"*.
   * Haga clic en **"Más información"** y luego en **"Ejecutar de todas formas"**.
3. La instalación se ejecutará de forma automática y rápida. **No requiere permisos de administrador**.

### Paso 3: Accesos Directos
Una vez finalizada la instalación, el sistema creará automáticamente:
* Un acceso directo en el **Escritorio** llamado `Homologador`.
* Un acceso directo en el **Menú Inicio** de Windows.

---

## 3. Primer Inicio de la Aplicación

1. Abra la aplicación haciendo doble clic en el acceso directo del **Escritorio**.
2. **Chequeo inicial automático:** La aplicación consultará en milisegundos si existe una versión más reciente. Si existe, la descargará y se reiniciará automáticamente.
3. Se desplegará la pantalla de autenticación (*Homologador carga masiva DDC*).
4. Ingrese con sus credenciales de usuario o correo corporativo.

---

## 4. Actualizaciones Automáticas Futuras

El usuario **no necesita volver a descargar ni reinstalar** la aplicación ante futuras mejoras o correcciones:

* Cada vez que se inicie el sistema, este detectará automáticamente si hay una nueva versión publicada en GitHub.
* La aplicación descargará únicamente los paquetes diferenciales (deltas de bajo peso) en segundo plano y se reiniciará en la versión actualizada de forma transparente.

---

## 5. Preguntas Frecuentes y Solución de Problemas

### La aplicación no inicia o solicita instalar .NET
* Asegúrese de tener instalado el **.NET 10 Desktop Runtime (x64)**. Puede descargarlo gratuitamente desde la página oficial de Microsoft.

### Error de conexión al servidor de base de datos
* Verifique que el servicio de SQL Server (`SQLEXPRESS` o el servidor correspondiente) esté en ejecución.
* Si el servidor se encuentra en red, verifique la conectividad y que las credenciales o cadena de conexión en el archivo de configuración sean las adecuadas.

### Bloqueo por antivirus corporativo
* Agregue una exclusión para la ruta de instalación local de la aplicación (ubicada por defecto en `%LocalAppData%\Homologador`).
