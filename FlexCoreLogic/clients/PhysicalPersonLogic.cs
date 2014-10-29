﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlexCoreDAOs.clients;
using FlexCoreDTOs.clients;
using FlexCoreLogic.exceptions;
using System.Data.SqlClient;
using ConexionSQLServer.SQLServerConnectionManager;

namespace FlexCoreLogic.clients
{
    class PhysicalPersonLogic:AbstractPersonLogic<PhysicalPersonDTO>
    {
        private static PhysicalPersonLogic _instance = null;
        private static object _syncLock = new object();

        public static PhysicalPersonLogic  getInstance(){
            if (_instance == null)
            {
                lock (_syncLock)
                {
                    if (_instance == null)
                    {
                        _instance = new PhysicalPersonLogic();
                    }
                }
            }
            return _instance;
        }

        private PhysicalPersonLogic() { }

        public override void insert(PhysicalPersonDTO pPerson)
        {
            SqlConnection con = SQLServerManager.newConnection();
            SqlCommand command = new SqlCommand();
            SqlTransaction tran = con.BeginTransaction();
            command.Connection = con;
            command.Transaction = tran;
            try
            {
                insert(pPerson, command);
                tran.Commit();
            }
            catch (Exception e)
            {
                tran.Rollback();
                throw e;
            }
            finally
            {
                SQLServerManager.closeConnection(con);
            }
        }

        public override void insert(PhysicalPersonDTO pPerson, SqlCommand pCommand)
        {
            try
            {
                PersonDAO perDao = PersonDAO.getInstance();
                PhysicalPersonDAO phyDao = PhysicalPersonDAO.getInstance();
                PersonDTO result = perDao.search(pPerson, pCommand)[0];
                if (result == null)
                {
                    perDao.insert(pPerson, pCommand);
                }
                phyDao.insert(pPerson, pCommand);
            }
            catch (SqlException e)
            {
                throw new InsertException();
            }
        }

        public override void delete(PhysicalPersonDTO  pPerson, SqlCommand pCommand)
        {
            try
            {
                PersonDAO dao = PersonDAO.getInstance();
                dao.delete(pPerson, pCommand);
            }
            catch (SqlException e)
            {
                throw new DeleteException();
            }
                
        }

        public override void update(PhysicalPersonDTO pNewPerson, PhysicalPersonDTO pPastPerson)
        {
            SqlConnection con = SQLServerManager.newConnection();
            SqlCommand command = new SqlCommand();
            SqlTransaction tran = con.BeginTransaction();
            command.Connection = con;
            command.Transaction = tran;
            try
            {
                update(pNewPerson, pPastPerson, command);
                tran.Commit();
            }
            catch (Exception e)
            {
                tran.Rollback();
                throw e;
            }
            finally
            {
                SQLServerManager.closeConnection(con);
            }
        }

        public override void update(PhysicalPersonDTO  pNewPerson, PhysicalPersonDTO  pPastPerson, SqlCommand pCommand)
        {
            try
            {
                PersonDAO perDao = PersonDAO.getInstance();
                perDao.update(pNewPerson, pPastPerson, pCommand);
                PhysicalPersonDAO phyDao = PhysicalPersonDAO.getInstance();
                phyDao.update(pNewPerson, pPastPerson, pCommand);
            }
            catch (SqlException e)
            {
                throw new UpdateException();
            }
            
        }

        public override List<PhysicalPersonDTO> search(PhysicalPersonDTO  pPerson, SqlCommand pCommand, int pPageNumber, int pShowCount, params string[] pOrderBy)
        {
            try
            {
                PhysicalPersonDAO dao = PhysicalPersonDAO.getInstance();
                return dao.search(pPerson, pCommand, pPageNumber, pShowCount, pOrderBy);
            }
            catch (SqlException e)
            {
                throw new SearchException();
            }
        
        }

        public override List<PhysicalPersonDTO > getAll(int pPageNumber, int pShowCount, params string[] pOrderBy)
        {
            try
            {
                PhysicalPersonDAO dao = PhysicalPersonDAO.getInstance();
                return dao.getAll(pPageNumber, pShowCount, pOrderBy);
            }
            catch (SqlException e)
            {
                throw new SearchException();
            }
            
        }
    }
}