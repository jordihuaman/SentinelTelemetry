using SentinelTelemetry.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SentinelTelemetry.Core.Interfaces
{
    public interface IEventRepository
    {
        Task SaveEventAsync(EventBase evento);
    }
}
