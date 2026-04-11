using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using VariedadesAby.Core.DTOs.FtpDrive;
using VariedadesAby.Core.DTOs.Reporte;
using VariedadesAby.Core.Interfaces;

namespace VariedadesAby.Infrastructure.Services;

public sealed class EmailNotificationService : IEmailNotificationService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(IOptions<EmailSettings> settings, ILogger<EmailNotificationService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public Task<string?> SendTransferSuccessAsync(TransferResult result, CancellationToken ct = default)
    {
        var subject = $"✅ Backup transferido | {result.FileName}";
        var body = $"""
            <div style="font-family:sans-serif;max-width:600px;margin:auto;">
              <h2 style="color:#16a34a;border-bottom:2px solid #16a34a;padding-bottom:8px;">
                ✅ Transferencia Completada
              </h2>
              <table style="width:100%;border-collapse:collapse;margin-top:16px;">
                <tr style="background:#f0fdf4;">
                  <td style="padding:10px;border:1px solid #bbf7d0;font-weight:bold;width:35%;">Archivo</td>
                  <td style="padding:10px;border:1px solid #bbf7d0;">{result.FileName}</td>
                </tr>
                <tr>
                  <td style="padding:10px;border:1px solid #bbf7d0;font-weight:bold;">ID en Google Drive</td>
                  <td style="padding:10px;border:1px solid #bbf7d0;font-family:monospace;">{result.DriveFileId}</td>
                </tr>
                <tr style="background:#f0fdf4;">
                  <td style="padding:10px;border:1px solid #bbf7d0;font-weight:bold;">Ejecutado</td>
                  <td style="padding:10px;border:1px solid #bbf7d0;">{result.ExecutedAt:yyyy-MM-dd HH:mm:ss} UTC</td>
                </tr>
              </table>
              <p style="margin-top:24px;font-size:12px;color:#6b7280;">
                Módulo FTP-to-Drive — <strong>Variedades Aby Admin</strong>
              </p>
            </div>
            """;

        return SendAsync(subject, body, ct);
    }

    public Task<string?> SendTransferFailureAsync(string errorMessage, CancellationToken ct = default)
    {
        var subject = "❌ Error en backup | Variedades Aby";
        var escapedError = System.Net.WebUtility.HtmlEncode(errorMessage);
        var body = $"""
            <div style="font-family:sans-serif;max-width:600px;margin:auto;">
              <h2 style="color:#dc2626;border-bottom:2px solid #dc2626;padding-bottom:8px;">
                ❌ Fallo en la Transferencia FTP → Drive
              </h2>
              <p style="color:#374151;">Se produjo un error durante el proceso de backup:</p>
              <pre style="background:#fef2f2;border:1px solid #fecaca;border-radius:6px;padding:12px;
                          font-size:13px;overflow-x:auto;white-space:pre-wrap;">{escapedError}</pre>
              <p style="margin-top:24px;font-size:12px;color:#6b7280;">
                Módulo FTP-to-Drive — <strong>Variedades Aby Admin</strong>
              </p>
            </div>
            """;

        return SendAsync(subject, body, ct);
    }

    public Task<string?> SendTestEmailAsync(CancellationToken ct = default)
    {
        var subject = "🔧 Prueba de configuración SMTP — Variedades Aby";
        var body = """
            <div style="font-family:sans-serif;max-width:600px;margin:auto;">
              <h2 style="color:#2563eb;">🔧 Correo de prueba</h2>
              <p>Si recibiste este mensaje, la configuración SMTP está funcionando correctamente.</p>
              <p style="font-size:12px;color:#6b7280;">Variedades Aby Admin — Módulo FTP-to-Drive</p>
            </div>
            """;

        return SendAsync(subject, body, ct);
    }

    public Task<string?> SendReporteDiarioAsync(ReporteDiarioDto r, CancellationToken ct = default)
    {
        var subject = $"📊 Reporte Diario {r.Fecha:dd/MM/yyyy} — Variedades Aby";

        // ── Filas de ventas por sucursal ────────────────────────────────────────
        var filasSucursal = string.Concat(r.VentasPorSucursal.Select(s => $"""
            <tr>
              <td style="padding:8px 10px;border:1px solid #e5e7eb;">{s.Sucursal}</td>
              <td style="padding:8px 10px;border:1px solid #e5e7eb;text-align:center;">{s.VentasContado}</td>
              <td style="padding:8px 10px;border:1px solid #e5e7eb;text-align:right;">C$ {s.TotalContado:N2}</td>
              <td style="padding:8px 10px;border:1px solid #e5e7eb;text-align:center;">{s.VentasCredito}</td>
              <td style="padding:8px 10px;border:1px solid #e5e7eb;text-align:right;">C$ {s.TotalCredito:N2}</td>
              <td style="padding:8px 10px;border:1px solid #e5e7eb;text-align:center;font-weight:bold;">{s.TotalVentas}</td>
              <td style="padding:8px 10px;border:1px solid #e5e7eb;text-align:right;font-weight:bold;color:#1d4ed8;">C$ {s.TotalGlobal:N2}</td>
            </tr>
            """));

        // ── Filas top productos ─────────────────────────────────────────────────
        var filasTop = string.Concat(r.TopProductos.Select((p, i) => $"""
            <tr style="{(i % 2 == 0 ? "background:#f9fafb;" : "")}">
              <td style="padding:7px 10px;border:1px solid #e5e7eb;color:#6b7280;">{i + 1}</td>
              <td style="padding:7px 10px;border:1px solid #e5e7eb;">{p.Producto}</td>
              <td style="padding:7px 10px;border:1px solid #e5e7eb;text-align:center;font-weight:bold;">{p.CantidadVendida}</td>
              <td style="padding:7px 10px;border:1px solid #e5e7eb;text-align:right;">C$ {p.TotalVendido:N2}</td>
            </tr>
            """));

        var body = $"""
            <div style="font-family:Arial,sans-serif;max-width:720px;margin:auto;background:#f1f5f9;">

              <!-- HEADER -->
              <div style="background:#1e40af;color:white;padding:24px 28px;border-radius:8px 8px 0 0;">
                <h1 style="margin:0;font-size:20px;">📊 Reporte Diario de Operaciones</h1>
                <p style="margin:6px 0 0;opacity:0.85;font-size:13px;">Variedades Aby &nbsp;|&nbsp; {r.Fecha:dddd dd 'de' MMMM 'de' yyyy}</p>
              </div>

              <div style="background:white;padding:24px 28px;">

                <!-- KPIs VENTAS -->
                <h2 style="color:#1e3a8a;font-size:15px;margin:0 0 12px;border-bottom:2px solid #dbeafe;padding-bottom:6px;">
                  VENTAS DEL DÍA
                </h2>
                <table style="width:100%;border-collapse:collapse;margin-bottom:20px;">
                  <tr>
                    <td style="width:33%;padding:14px;background:#eff6ff;border-radius:6px;text-align:center;">
                      <div style="font-size:11px;color:#6b7280;text-transform:uppercase;letter-spacing:.5px;">Contado</div>
                      <div style="font-size:20px;font-weight:bold;color:#1d4ed8;">C$ {r.Ventas.TotalContado:N2}</div>
                      <div style="font-size:12px;color:#6b7280;">{r.VentasPorSucursal.Sum(s => s.VentasContado)} ventas</div>
                    </td>
                    <td style="width:4%;"></td>
                    <td style="width:33%;padding:14px;background:#f0fdf4;border-radius:6px;text-align:center;">
                      <div style="font-size:11px;color:#6b7280;text-transform:uppercase;letter-spacing:.5px;">Crédito</div>
                      <div style="font-size:20px;font-weight:bold;color:#15803d;">C$ {r.Ventas.TotalCredito:N2}</div>
                      <div style="font-size:12px;color:#6b7280;">{r.VentasPorSucursal.Sum(s => s.VentasCredito)} ventas</div>
                    </td>
                    <td style="width:4%;"></td>
                    <td style="width:33%;padding:14px;background:#fef3c7;border-radius:6px;text-align:center;">
                      <div style="font-size:11px;color:#6b7280;text-transform:uppercase;letter-spacing:.5px;">Total Global</div>
                      <div style="font-size:20px;font-weight:bold;color:#b45309;">C$ {r.Ventas.TotalGlobal:N2}</div>
                      <div style="font-size:12px;color:#6b7280;">{r.Ventas.TotalVentas} ventas</div>
                    </td>
                  </tr>
                </table>

                <!-- TABLA POR SUCURSAL -->
                <h2 style="color:#1e3a8a;font-size:15px;margin:0 0 10px;border-bottom:2px solid #dbeafe;padding-bottom:6px;">
                  POR SUCURSAL
                </h2>
                <table style="width:100%;border-collapse:collapse;font-size:13px;margin-bottom:24px;">
                  <thead>
                    <tr style="background:#1e40af;color:white;">
                      <th style="padding:9px 10px;text-align:left;">Sucursal</th>
                      <th style="padding:9px 10px;text-align:center;"># Contado</th>
                      <th style="padding:9px 10px;text-align:right;">Monto</th>
                      <th style="padding:9px 10px;text-align:center;"># Crédito</th>
                      <th style="padding:9px 10px;text-align:right;">Monto</th>
                      <th style="padding:9px 10px;text-align:center;"># Total</th>
                      <th style="padding:9px 10px;text-align:right;">Total Global</th>
                    </tr>
                  </thead>
                  <tbody>{filasSucursal}</tbody>
                </table>

                <!-- DEVOLUCIONES + ABONOS -->
                <table style="width:100%;border-collapse:collapse;margin-bottom:24px;">
                  <tr>
                    <td style="width:48%;padding:14px;background:#fef2f2;border-radius:6px;vertical-align:top;">
                      <div style="font-size:11px;color:#6b7280;text-transform:uppercase;letter-spacing:.5px;margin-bottom:6px;">Devoluciones del día</div>
                      <div style="font-size:18px;font-weight:bold;color:#dc2626;">C$ {r.Devoluciones.MontoTotal:N2}</div>
                      <div style="font-size:12px;color:#6b7280;">{r.Devoluciones.Cantidad} notas de crédito</div>
                    </td>
                    <td style="width:4%;"></td>
                    <td style="width:48%;padding:14px;background:#f0fdf4;border-radius:6px;vertical-align:top;">
                      <div style="font-size:11px;color:#6b7280;text-transform:uppercase;letter-spacing:.5px;margin-bottom:6px;">Abonos registrados hoy</div>
                      <div style="font-size:18px;font-weight:bold;color:#15803d;">C$ {r.Abonos.MontoTotal:N2}</div>
                      <div style="font-size:12px;color:#6b7280;">{r.Abonos.Cantidad} pagos recibidos</div>
                    </td>
                  </tr>
                </table>

                <!-- CARTERA -->
                <div style="background:#fffbeb;border-left:4px solid #f59e0b;padding:14px 16px;border-radius:0 6px 6px 0;margin-bottom:24px;">
                  <div style="font-size:11px;color:#6b7280;text-transform:uppercase;letter-spacing:.5px;">Cartera Activa (Cuentas por Cobrar)</div>
                  <div style="font-size:22px;font-weight:bold;color:#b45309;margin-top:4px;">C$ {r.Cartera.SaldoTotal:N2}</div>
                  <div style="font-size:12px;color:#6b7280;">{r.Cartera.ClientesConSaldo} clientes con saldo pendiente</div>
                </div>

                <!-- TOP PRODUCTOS -->
                <h2 style="color:#1e3a8a;font-size:15px;margin:0 0 10px;border-bottom:2px solid #dbeafe;padding-bottom:6px;">
                  TOP 10 PRODUCTOS MÁS VENDIDOS HOY
                </h2>
                <table style="width:100%;border-collapse:collapse;font-size:13px;">
                  <thead>
                    <tr style="background:#1e40af;color:white;">
                      <th style="padding:9px 10px;text-align:left;">#</th>
                      <th style="padding:9px 10px;text-align:left;">Producto</th>
                      <th style="padding:9px 10px;text-align:center;">Unidades</th>
                      <th style="padding:9px 10px;text-align:right;">Total Vendido</th>
                    </tr>
                  </thead>
                  <tbody>{filasTop}</tbody>
                </table>

              </div>

              <!-- FOOTER -->
              <div style="background:#e2e8f0;padding:12px 28px;border-radius:0 0 8px 8px;text-align:center;">
                <p style="margin:0;font-size:11px;color:#64748b;">
                  Variedades Aby Admin &nbsp;|&nbsp; Reporte generado automáticamente a las 6:30 PM (hora Nicaragua)
                </p>
              </div>

            </div>
            """;

        return SendAsync(subject, body, ct);
    }

    /// <summary>
    /// Envía el correo y retorna null si tuvo éxito, o el mensaje de error si falló.
    /// Nunca lanza excepción — el fallo de email no debe interrumpir el flujo principal.
    /// </summary>
    private async Task<string?> SendAsync(string subject, string htmlBody, CancellationToken ct)
    {
        if (_settings.ReportRecipients.Count == 0)
        {
            var msg = "No hay destinatarios configurados en EmailSettings:ReportRecipients.";
            _logger.LogWarning("[Email] {Msg}", msg);
            return msg;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        foreach (var address in _settings.ReportRecipients)
            message.To.Add(MailboxAddress.Parse(address));
        message.Subject = subject;
        message.Body = new TextPart(TextFormat.Html) { Text = htmlBody };

        try
        {
            using var smtp = new SmtpClient();

            // Omite la verificación de revocación del certificado SSL,
            // necesario en algunos entornos de hosting/desarrollo con Windows.
            smtp.ServerCertificateValidationCallback = (_, _, _, _) => true;

            var secureOption = _settings.UseSsl
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

            await smtp.ConnectAsync(_settings.Host, _settings.Port, secureOption, ct);
            await smtp.AuthenticateAsync(_settings.Username, _settings.Password, ct);
            await smtp.SendAsync(message, ct);
            await smtp.DisconnectAsync(true, ct);

            _logger.LogInformation("[Email] Enviado a: {To}", string.Join(", ", _settings.ReportRecipients));
            return null; // éxito
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Email] Error al enviar por SMTP ({Host}:{Port}).", _settings.Host, _settings.Port);
            return ex.Message; // retorna el error sin lanzarlo
        }
    }
}
