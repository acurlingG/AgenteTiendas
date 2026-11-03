using AgenteTiendas.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;


namespace AgenteTiendas.Services;
public sealed class ConfigurationReader
{
    public StoreConfiguration Read(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"No se encontró el archivo de configuración: {filePath}");
        }

        var json = File.ReadAllText(filePath);

        var configuration = JsonSerializer.Deserialize<StoreConfiguration>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (configuration is null)
        {
            throw new InvalidDataException(
                "No se pudo leer la configuración JSON.");
        }

        if (string.IsNullOrWhiteSpace(configuration.Codigo))
        {
            throw new InvalidDataException(
                "Falta el campo Codigo en config.json.");
        }

        if (string.IsNullOrWhiteSpace(configuration.Nombre))
        {
            throw new InvalidDataException(
                "Falta el campo Nombre en config.json.");
        }

        if (configuration.IntervaloReporteSegundos < 5)
        {
            configuration.IntervaloReporteSegundos = 30;
        }

        return configuration;
    }
}