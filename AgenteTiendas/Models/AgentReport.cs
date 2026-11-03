using System;
using System.Collections.Generic;
using System.Text;

namespace AgenteTiendas.Models
{
    public sealed record AgentReport(
    string StoreCode,
    string StoreName,
    string WindowsVersion,
    string WindowsEdition,
    PlayerStatus Player,
    DateTimeOffset ReportedAt);
}
