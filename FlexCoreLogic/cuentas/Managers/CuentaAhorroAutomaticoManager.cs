﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using FlexCoreDTOs.cuentas;
using MySql.Data.MySqlClient;
using ConexionMySQLServer.ConexionMySql;
using FlexCoreDAOs.cuentas;
using FlexCoreLogic.cuentas.Generales;

namespace FlexCoreLogic.cuentas.Managers
{
    internal static class CuentaAhorroAutomaticoManager
    {
        public static int SLEEP = 1000;

        private static MySqlCommand obtenerConexionSQL()
        {
            MySqlConnection _conexionMySQLBase = MySQLManager.nuevaConexion();
            MySqlCommand _comandoMySQL = _conexionMySQLBase.CreateCommand();
            MySqlTransaction _transaccion = _conexionMySQLBase.BeginTransaction();
            _comandoMySQL.Connection = _conexionMySQLBase;
            _comandoMySQL.Transaction = _transaccion;
            return _comandoMySQL;
        }

        public static string agregarCuentaAhorroAutomatico(CuentaAhorroAutomaticoDTO pCuentaAhorroAutomatico)
        {
            MySqlCommand _comandoMySQL = obtenerConexionSQL();
            try
            {
                string _numeroCuenta = GeneradorCuentas.generarCuenta(Constantes.AHORROAUTOMATICO, pCuentaAhorroAutomatico.getTipoMoneda(), _comandoMySQL);
                DateTime _fechaFinalizacion = pCuentaAhorroAutomatico.getFechaInicio().AddMonths(pCuentaAhorroAutomatico.getTiempoAhorro());
                decimal _montoAhorro = calcularMontoAhorro(pCuentaAhorroAutomatico.getTiempoAhorro(), pCuentaAhorroAutomatico.getMagnitudPeriodoAhorro(), pCuentaAhorroAutomatico.getTipoPeriodo(), pCuentaAhorroAutomatico.getMontoDeduccion());
                pCuentaAhorroAutomatico.setNumeroCuenta(_numeroCuenta);
                pCuentaAhorroAutomatico.setSaldo(0);
                pCuentaAhorroAutomatico.setEstado(false);
                pCuentaAhorroAutomatico.setFechaFinalizacion(_fechaFinalizacion.Day, _fechaFinalizacion.Month, _fechaFinalizacion.Year, _fechaFinalizacion.Hour, _fechaFinalizacion.Minute, _fechaFinalizacion.Second);
                pCuentaAhorroAutomatico.setMontoAhorro(_montoAhorro);
                pCuentaAhorroAutomatico.setUltimaFechaCobro(pCuentaAhorroAutomatico.getFechaInicio().Day, pCuentaAhorroAutomatico.getFechaInicio().Month, pCuentaAhorroAutomatico.getFechaInicio().Year, pCuentaAhorroAutomatico.getFechaInicio().Hour, pCuentaAhorroAutomatico.getFechaInicio().Minute, pCuentaAhorroAutomatico.getFechaInicio().Second);
                CuentaAhorroAutomaticoDAO.agregarCuentaAhorroAutomaticoBase(pCuentaAhorroAutomatico, _comandoMySQL);
                _comandoMySQL.Transaction.Commit();
                Console.WriteLine(iniciarAhorro(pCuentaAhorroAutomatico));
                return "Transacción completada con éxito";
            }
            catch
            {
                try
                {
                    _comandoMySQL.Transaction.Rollback();
                    return "Ha ocurrido un error en la transacción";
                }
                catch
                {
                    return "Ha ocurrido un error en la transacción";
                }
            }
            finally
            {
                MySQLManager.cerrarConexion(_comandoMySQL.Connection);
            }
        }

        public static string iniciarAhorro(CuentaAhorroAutomaticoDTO pCuentaAhorroAutomatico)
        {
            try
            {
                ThreadStart _delegado = new ThreadStart(() => esperarTiempoInicioAhorro(pCuentaAhorroAutomatico));
                Thread _hiloReplica = new Thread(_delegado);
                _hiloReplica.Start();
                return "Transacción completada con éxito";
            }
            catch
            {
                return "Ha ocurrido un error en la transacción";
            }
            
        }

        private static void esperarTiempoInicioAhorro(CuentaAhorroAutomaticoDTO pCuentaAhorroAutomatico)
        {
            while(Tiempo.getHoraActual() < pCuentaAhorroAutomatico.getFechaInicio())
            {
                Thread.Sleep(SLEEP);
            }
            modificarEstadoCuentaAhorroAutomatico(pCuentaAhorroAutomatico, true);
            iniciarAhorroAux(pCuentaAhorroAutomatico);
        }

        private static void modificarEstadoCuentaAhorroAutomatico(CuentaAhorroAutomaticoDTO pCuentaAhorroAutomatico, bool pEstado)
        {
            MySqlCommand _comandoMySQL = obtenerConexionSQL();
            try
            {
                pCuentaAhorroAutomatico.setEstado(pEstado);
                CuentaAhorroDAO.modificarCuentaAhorro(pCuentaAhorroAutomatico, _comandoMySQL);
                _comandoMySQL.Transaction.Commit();
            }
            catch
            {
                try
                {
                    pCuentaAhorroAutomatico.setEstado(!pEstado);
                    _comandoMySQL.Transaction.Rollback();
                }
                catch
                {
                    return;
                }
            }
            finally
            {
                MySQLManager.cerrarConexion(_comandoMySQL.Connection);
            }
        }

        private static void iniciarAhorroAux(CuentaAhorroAutomaticoDTO pCuentaAhorroAutomatico)
        {
            if (pCuentaAhorroAutomatico.getTipoPeriodo() == Constantes.SEGUNDOS)
            {
                cobrarEnSegundos(pCuentaAhorroAutomatico);
            }
            else if (pCuentaAhorroAutomatico.getTipoPeriodo() == Constantes.MINUTOS)
            {
                cobrarEnMinutos(pCuentaAhorroAutomatico);
            }
            else if (pCuentaAhorroAutomatico.getTipoPeriodo() == Constantes.HORAS)
            {
                cobrarEnHoras(pCuentaAhorroAutomatico);
            }
            else if (pCuentaAhorroAutomatico.getTipoPeriodo() == Constantes.DIAS)
            {
                cobrarEnDias(pCuentaAhorroAutomatico);
            }
        }

        private static void cobrarEnSegundos(CuentaAhorroAutomaticoDTO pCuentaAhorroAutomatico)
        {
            while (pCuentaAhorroAutomatico.getUltimaFechaCobro() < pCuentaAhorroAutomatico.getFechaFinalizacion())
            {
                if (pCuentaAhorroAutomatico.getUltimaFechaCobro().AddSeconds(pCuentaAhorroAutomatico.getMagnitudPeriodoAhorro()) < Tiempo.getHoraActual())
                {
                    DateTime _horaActualLimitada = getHoraActualLimitada(pCuentaAhorroAutomatico);
                    TimeSpan _tiempoTranscurrido = _horaActualLimitada - pCuentaAhorroAutomatico.getUltimaFechaCobro();
                    int _proporcionalidadDeCobro = Convert.ToInt32(Math.Truncate(_tiempoTranscurrido.TotalSeconds / pCuentaAhorroAutomatico.getMagnitudPeriodoAhorro()));
                    decimal _montoAAhorrar = _proporcionalidadDeCobro * pCuentaAhorroAutomatico.getMontoDeduccion();
                    CuentaAhorroVistaDTO _cuentaDeduccion = new CuentaAhorroVistaDTO();
                    _cuentaDeduccion.setNumeroCuenta(pCuentaAhorroAutomatico.getNumeroCuentaDeduccion());
                    realizarAhorro(_cuentaDeduccion, _montoAAhorrar, pCuentaAhorroAutomatico);
                    modificarUltimaFechaCobro(pCuentaAhorroAutomatico, _horaActualLimitada, _proporcionalidadDeCobro);
                }
                pCuentaAhorroAutomatico = obtenerCuentaAhorroAutomaticoNumeroCuenta(pCuentaAhorroAutomatico);
                Thread.Sleep(SLEEP);
            }
            modificarEstadoCuentaAhorroAutomatico(pCuentaAhorroAutomatico, false);
        }

        private static void cobrarEnDias(CuentaAhorroAutomaticoDTO pCuentaAhorroAutomatico)
        {
            while (pCuentaAhorroAutomatico.getUltimaFechaCobro() < pCuentaAhorroAutomatico.getFechaFinalizacion())
            {
                if (pCuentaAhorroAutomatico.getUltimaFechaCobro().AddDays(pCuentaAhorroAutomatico.getMagnitudPeriodoAhorro()) < Tiempo.getHoraActual())
                {
                    DateTime _horaActualLimitada = getHoraActualLimitada(pCuentaAhorroAutomatico);
                    TimeSpan _tiempoTranscurrido = _horaActualLimitada - pCuentaAhorroAutomatico.getUltimaFechaCobro();
                    int _proporcionalidadDeCobro = Convert.ToInt32(Math.Truncate(_tiempoTranscurrido.TotalDays / pCuentaAhorroAutomatico.getMagnitudPeriodoAhorro()));
                    decimal _montoAAhorrar = _proporcionalidadDeCobro * pCuentaAhorroAutomatico.getMontoDeduccion();
                    CuentaAhorroVistaDTO _cuentaDeduccion = new CuentaAhorroVistaDTO();
                    _cuentaDeduccion.setNumeroCuenta(pCuentaAhorroAutomatico.getNumeroCuentaDeduccion());
                    realizarAhorro(_cuentaDeduccion, _montoAAhorrar, pCuentaAhorroAutomatico);
                    modificarUltimaFechaCobro(pCuentaAhorroAutomatico, _horaActualLimitada, _proporcionalidadDeCobro);
                }
                pCuentaAhorroAutomatico = obtenerCuentaAhorroAutomaticoNumeroCuenta(pCuentaAhorroAutomatico);
                Thread.Sleep(SLEEP);
            }
            modificarEstadoCuentaAhorroAutomatico(pCuentaAhorroAutomatico, false);
        }

        private static void cobrarEnMinutos(CuentaAhorroAutomaticoDTO pCuentaAhorroAutomatico)
        {
            while (pCuentaAhorroAutomatico.getUltimaFechaCobro() < pCuentaAhorroAutomatico.getFechaFinalizacion())
            {
                if (pCuentaAhorroAutomatico.getUltimaFechaCobro().AddMinutes(pCuentaAhorroAutomatico.getMagnitudPeriodoAhorro()) < Tiempo.getHoraActual())
                {
                    DateTime _horaActualLimitada = getHoraActualLimitada(pCuentaAhorroAutomatico);
                    TimeSpan _tiempoTranscurrido = _horaActualLimitada - pCuentaAhorroAutomatico.getUltimaFechaCobro();
                    int _proporcionalidadDeCobro = Convert.ToInt32(Math.Truncate(_tiempoTranscurrido.TotalMinutes / pCuentaAhorroAutomatico.getMagnitudPeriodoAhorro()));
                    decimal _montoAAhorrar = _proporcionalidadDeCobro * pCuentaAhorroAutomatico.getMontoDeduccion();
                    CuentaAhorroVistaDTO _cuentaDeduccion = new CuentaAhorroVistaDTO();
                    _cuentaDeduccion.setNumeroCuenta(pCuentaAhorroAutomatico.getNumeroCuentaDeduccion());
                    realizarAhorro(_cuentaDeduccion, _montoAAhorrar, pCuentaAhorroAutomatico);
                    modificarUltimaFechaCobro(pCuentaAhorroAutomatico, _horaActualLimitada, _proporcionalidadDeCobro);
                }
                pCuentaAhorroAutomatico = obtenerCuentaAhorroAutomaticoNumeroCuenta(pCuentaAhorroAutomatico);
                Thread.Sleep(SLEEP);
            }
            modificarEstadoCuentaAhorroAutomatico(pCuentaAhorroAutomatico, false);
        }

        private static void cobrarEnHoras(CuentaAhorroAutomaticoDTO pCuentaAhorroAutomatico)
        {
            while (pCuentaAhorroAutomatico.getUltimaFechaCobro() < pCuentaAhorroAutomatico.getFechaFinalizacion())
            {
                if (pCuentaAhorroAutomatico.getUltimaFechaCobro().AddHours(pCuentaAhorroAutomatico.getMagnitudPeriodoAhorro()) < Tiempo.getHoraActual())
                {
                    DateTime _horaActualLimitada = getHoraActualLimitada(pCuentaAhorroAutomatico);
                    TimeSpan _tiempoTranscurrido = _horaActualLimitada - pCuentaAhorroAutomatico.getUltimaFechaCobro();
                    int _proporcionalidadDeCobro = Convert.ToInt32(Math.Truncate(_tiempoTranscurrido.TotalHours / pCuentaAhorroAutomatico.getMagnitudPeriodoAhorro()));
                    decimal _montoAAhorrar = _proporcionalidadDeCobro * pCuentaAhorroAutomatico.getMontoDeduccion();
                    CuentaAhorroVistaDTO _cuentaDeduccion = new CuentaAhorroVistaDTO();
                    _cuentaDeduccion.setNumeroCuenta(pCuentaAhorroAutomatico.getNumeroCuentaDeduccion());
                    realizarAhorro(_cuentaDeduccion, _montoAAhorrar, pCuentaAhorroAutomatico);
                    modificarUltimaFechaCobro(pCuentaAhorroAutomatico, _horaActualLimitada, _proporcionalidadDeCobro);
                }
                pCuentaAhorroAutomatico = obtenerCuentaAhorroAutomaticoNumeroCuenta(pCuentaAhorroAutomatico);
                Thread.Sleep(SLEEP);
            }
            modificarEstadoCuentaAhorroAutomatico(pCuentaAhorroAutomatico, false);
        }

        private static void modificarUltimaFechaCobro(CuentaAhorroAutomaticoDTO pCuentaAhorroAutomatico, DateTime pHoraActual, int pProporcionalidadDeCobro)
        {
            MySqlCommand _comandoMySQL = obtenerConexionSQL();
            DateTime _ultimaFechaCobro = new DateTime();
            if(pHoraActual == pCuentaAhorroAutomatico.getFechaFinalizacion())
            {
                _ultimaFechaCobro = pCuentaAhorroAutomatico.getFechaFinalizacion();
            }
            else if (pCuentaAhorroAutomatico.getTipoPeriodo() == Constantes.SEGUNDOS)
            {
                _ultimaFechaCobro = pCuentaAhorroAutomatico.getUltimaFechaCobro().AddSeconds(pProporcionalidadDeCobro * pCuentaAhorroAutomatico.getMagnitudPeriodoAhorro());
            }
            else if (pCuentaAhorroAutomatico.getTipoPeriodo() == Constantes.MINUTOS)
            {
                _ultimaFechaCobro = pCuentaAhorroAutomatico.getUltimaFechaCobro().AddMinutes(pProporcionalidadDeCobro * pCuentaAhorroAutomatico.getMagnitudPeriodoAhorro());
            }
            else if (pCuentaAhorroAutomatico.getTipoPeriodo() == Constantes.HORAS)
            {
                _ultimaFechaCobro = pCuentaAhorroAutomatico.getUltimaFechaCobro().AddHours(pProporcionalidadDeCobro * pCuentaAhorroAutomatico.getMagnitudPeriodoAhorro());
            }
            else if (pCuentaAhorroAutomatico.getTipoPeriodo() == Constantes.DIAS)
            {
                _ultimaFechaCobro = pCuentaAhorroAutomatico.getUltimaFechaCobro().AddDays(pProporcionalidadDeCobro * pCuentaAhorroAutomatico.getMagnitudPeriodoAhorro());
            }
            pCuentaAhorroAutomatico.setUltimaFechaCobro(_ultimaFechaCobro.Day, _ultimaFechaCobro.Month, _ultimaFechaCobro.Year, _ultimaFechaCobro.Hour, _ultimaFechaCobro.Minute, _ultimaFechaCobro.Second);
            try
            {
                CuentaAhorroAutomaticoDAO.modificarUltimaFechaCobro(pCuentaAhorroAutomatico, pCuentaAhorroAutomatico.getUltimaFechaCobro(), _comandoMySQL);
                _comandoMySQL.Transaction.Commit();
            }
            catch
            {
                try
                {
                    _comandoMySQL.Transaction.Rollback();
                }
                catch
                {
                    return;
                }
            }
            finally
            {
                MySQLManager.cerrarConexion(_comandoMySQL.Connection);
            }
        }

        private static DateTime getHoraActualLimitada(CuentaAhorroAutomaticoDTO pCuentaAhorroAutomatico)
        {
            if(pCuentaAhorroAutomatico.getFechaFinalizacion() < Tiempo.getHoraActual())
            {
                return pCuentaAhorroAutomatico.getFechaFinalizacion();
            }
            else
            {
                return Tiempo.getHoraActual();
            }
        }

        private static void realizarAhorro(CuentaAhorroVistaDTO pCuentaOrigen, decimal pMontoAhorro, CuentaAhorroAutomaticoDTO pCuentaDestino)
        {
            MySqlCommand _comandoMySQL = obtenerConexionSQL();
            try
            {
                CuentaAhorroVistaDTO _cuentaOrigen = CuentaAhorroVistaDAO.obtenerCuentaAhorroVistaNumeroCuenta(pCuentaOrigen, _comandoMySQL);
                if (_cuentaOrigen.getEstado() == false)
                {
                    Console.WriteLine("La cuenta desde donde se hace la deduccion se encuentra desactivada");
                    //GENERO EL ERROR A LA TABLA DE ERRORES.
                }
                else if (_cuentaOrigen.getSaldoFlotante() < pMontoAhorro)
                {
                    Console.WriteLine("La cuenta desde donde se hace la deduccion se ha quedado sin fondos");
                    //SE GENERA EL ERROR A LA TABLA DE ERRORES
                }
                else
                {
                    CuentaAhorroVistaDAO.quitarDinero(pCuentaOrigen, pMontoAhorro, pCuentaDestino, Constantes.AHORROAUTOMATICO, _comandoMySQL);
                    _comandoMySQL.Transaction.Commit();
                }
            }
            catch
            {
                try
                {
                    _comandoMySQL.Transaction.Rollback();
                }
                catch
                {
                    return;
                }
            }
            finally
            {
                MySQLManager.cerrarConexion(_comandoMySQL.Connection);
            }
        }

        public static string eliminarCuentaAhorroAutomatico(CuentaAhorroAutomaticoDTO pCuentaAhorroAutomatico)
        {
            MySqlCommand _comandoMySQL = obtenerConexionSQL();
            try
            {
                CuentaAhorroAutomaticoDAO.eliminarCuentaAhorroAutomaticoBase(pCuentaAhorroAutomatico, _comandoMySQL);
                _comandoMySQL.Transaction.Commit();
                return "Transacción completada con éxito";
            }
            catch
            {
                try
                {
                    _comandoMySQL.Transaction.Rollback();
                    return "Ha ocurrido un error en la transacción";
                }
                catch
                {
                    return "Ha ocurrido un error en la transacción";
                }
            }
            finally
            {
                MySQLManager.cerrarConexion(_comandoMySQL.Connection);
            }
        }

        public static string modificarCuentaAhorroAutomatico(CuentaAhorroAutomaticoDTO pCuentaAhorroAutomatico)
        {
            MySqlCommand _comandoMySQL = obtenerConexionSQL();
            try
            {
                CuentaAhorroAutomaticoDTO _cuentaAhorroAutomaticoInterna = obtenerCuentaAhorroAutomaticoNumeroCuenta(pCuentaAhorroAutomatico);
                decimal _montoAhorro = calcularMontoAhorro(pCuentaAhorroAutomatico.getTiempoAhorro(), pCuentaAhorroAutomatico.getMagnitudPeriodoAhorro(), pCuentaAhorroAutomatico.getTipoPeriodo(), pCuentaAhorroAutomatico.getMontoDeduccion());
                DateTime _fechaFinalizacion = _cuentaAhorroAutomaticoInterna.getFechaInicio().AddMonths(pCuentaAhorroAutomatico.getTiempoAhorro());
                _cuentaAhorroAutomaticoInterna.setMontoAhorro(_montoAhorro);
                _cuentaAhorroAutomaticoInterna.setFechaFinalizacion(_fechaFinalizacion.Day, _fechaFinalizacion.Month, _fechaFinalizacion.Year, _fechaFinalizacion.Hour, _fechaFinalizacion.Minute, _fechaFinalizacion.Second);
                _cuentaAhorroAutomaticoInterna.setDescripcion(pCuentaAhorroAutomatico.getDescripcion());
                _cuentaAhorroAutomaticoInterna.setTipoMoneda(pCuentaAhorroAutomatico.getTipoMoneda());
                _cuentaAhorroAutomaticoInterna.setTiempoAhorro(pCuentaAhorroAutomatico.getTiempoAhorro());
                _cuentaAhorroAutomaticoInterna.setMontoDeduccion(pCuentaAhorroAutomatico.getMontoDeduccion());
                _cuentaAhorroAutomaticoInterna.setProposito(pCuentaAhorroAutomatico.getProposito());
                _cuentaAhorroAutomaticoInterna.setMagnitudPeriodoAhorro(pCuentaAhorroAutomatico.getMagnitudPeriodoAhorro());
                _cuentaAhorroAutomaticoInterna.setTipoPeriodo(pCuentaAhorroAutomatico.getTipoPeriodo());
                _cuentaAhorroAutomaticoInterna.setNumeroCuentaDeduccion(pCuentaAhorroAutomatico.getNumeroCuentaDeduccion());
                CuentaAhorroAutomaticoDAO.modificarCuentaAhorroAutomaticoBase(_cuentaAhorroAutomaticoInterna, _comandoMySQL);
                _comandoMySQL.Transaction.Commit();
                return "Transacción completada con éxito";
            }
            catch
            {
                try
                {
                    _comandoMySQL.Transaction.Rollback();
                    return "Ha ocurrido un error en la transacción";
                }
                catch
                {
                    return "Ha ocurrido un error en la transacción";
                }
            }
            finally
            {
                MySQLManager.cerrarConexion(_comandoMySQL.Connection);
            }
        }

        public static CuentaAhorroAutomaticoDTO obtenerCuentaAhorroAutomaticoNumeroCuenta(CuentaAhorroAutomaticoDTO pCuentaAhorroAutomatico)
        {
            MySqlCommand _comandoMySQL = obtenerConexionSQL();
            try
            {
                CuentaAhorroAutomaticoDTO _cuentaSalida = CuentaAhorroAutomaticoDAO.obtenerCuentaAhorroAutomaticoNumeroCuenta(pCuentaAhorroAutomatico, _comandoMySQL);
                _comandoMySQL.Transaction.Commit();
                return _cuentaSalida;
            }
            catch
            {
                try
                {
                    _comandoMySQL.Transaction.Rollback();
                    return null;
                }
                catch
                {
                    return null;
                }
            }
            finally
            {
                MySQLManager.cerrarConexion(_comandoMySQL.Connection);
            }
        }

        public static List<CuentaAhorroAutomaticoDTO> obtenerCuentaAhorroAutomaticoCedula(CuentaAhorroAutomaticoDTO pCuentaAhorroAutomatico)
        {
            MySqlCommand _comandoMySQL = obtenerConexionSQL();
            try
            {
                List<CuentaAhorroAutomaticoDTO> _cuentasSalida = CuentaAhorroAutomaticoDAO.obtenerCuentaAhorroAutomaticoCedulaOCIF(pCuentaAhorroAutomatico, _comandoMySQL);
                _comandoMySQL.Transaction.Commit();
                return _cuentasSalida;
            }
            catch
            {
                try
                {
                    _comandoMySQL.Transaction.Rollback();
                    return null;
                }
                catch
                {
                    return null;
                }
            }
            finally
            {
                MySQLManager.cerrarConexion(_comandoMySQL.Connection);
            }
        }

        public static List<CuentaAhorroAutomaticoDTO> obtenerCuentaAhorroAutomaticoCIF(CuentaAhorroAutomaticoDTO pCuentaAhorroAutomatico)
        {
            MySqlCommand _comandoMySQL = obtenerConexionSQL();
            try
            {
                List<CuentaAhorroAutomaticoDTO> _cuentasSalida = CuentaAhorroAutomaticoDAO.obtenerCuentaAhorroAutomaticoCedulaOCIF(pCuentaAhorroAutomatico, _comandoMySQL);
                _comandoMySQL.Transaction.Commit();
                return _cuentasSalida;
            }
            catch
            {
                try
                {
                    _comandoMySQL.Transaction.Rollback();
                    return null;
                }
                catch
                {
                    return null;
                }
            }
            finally
            {
                MySQLManager.cerrarConexion(_comandoMySQL.Connection);
            }
        }

        private static decimal calcularMontoAhorro(int pTiempoAhorro, int pMagnitudPeriodoAhorro, int pTipoPeriodo, decimal pMontoDeduccion)
        {
            decimal _montoAhorro = 0;
            if (pTipoPeriodo == Constantes.SEGUNDOS)
            {
                _montoAhorro = Math.Truncate(((pTiempoAhorro) / (Tiempo.segundosAMeses(pMagnitudPeriodoAhorro)))) * pMontoDeduccion;
            }
            else if (pTipoPeriodo == Constantes.MINUTOS)
            {
                _montoAhorro = Math.Truncate(((pTiempoAhorro) / (Tiempo.minutosAMeses(pMagnitudPeriodoAhorro)))) * pMontoDeduccion;
            }
            else if (pTipoPeriodo == Constantes.HORAS)
            {
                _montoAhorro = Math.Truncate(((pTiempoAhorro) / (Tiempo.horasAMeses(pMagnitudPeriodoAhorro)))) * pMontoDeduccion;
            }
            else if (pTipoPeriodo == Constantes.DIAS)
            {
                _montoAhorro = Math.Truncate(((pTiempoAhorro) / (Tiempo.diasAMeses(pMagnitudPeriodoAhorro)))) * pMontoDeduccion;
            }
            return _montoAhorro;
        }
    }
}