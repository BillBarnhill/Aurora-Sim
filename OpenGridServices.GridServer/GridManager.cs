using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using OpenGrid.Framework.Data;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Console;
using OpenSim.Framework.Sims;
using libsecondlife;
using Nwc.XmlRpc;
using System.Xml;

namespace OpenGridServices.GridServer
{
    class GridManager
    {
        Dictionary<string, IGridData> _plugins = new Dictionary<string, IGridData>();

        /// <summary>
        /// Adds a new grid server plugin - grid servers will be requested in the order they were loaded.
        /// </summary>
        /// <param name="FileName">The filename to the grid server plugin DLL</param>
        public void AddPlugin(string FileName)
		{
			Assembly pluginAssembly = Assembly.LoadFrom(FileName);
			
			foreach (Type pluginType in pluginAssembly.GetTypes())
			{
				if (pluginType.IsPublic) 
				{
					if (!pluginType.IsAbstract)  
					{
                        Type typeInterface = pluginType.GetInterface("IGridData", true);
						
						if (typeInterface != null)
						{
                            IGridData plug = (IGridData)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                            plug.Initialise();
							this._plugins.Add(plug.getName(),plug);
							
						}	
						
						typeInterface = null; 			
					}				
				}			
			}
			
			pluginAssembly = null; 
        }
        
        /// <summary>
        /// Returns a region by argument
        /// </summary>
        /// <param name="uuid">A UUID key of the region to return</param>
        /// <returns>A SimProfileData for the region</returns>
        public SimProfileData getRegion(libsecondlife.LLUUID uuid)
        {
            foreach(KeyValuePair<string,IGridData> kvp in _plugins) {
                try
                {
                    return kvp.Value.GetProfileByLLUUID(uuid);
                }
                catch (Exception e)
                {
                    OpenSim.Framework.Console.MainConsole.Instance.WriteLine("getRegionPlugin UUID " + kvp.Key + " is made of fail: " + e.ToString());
                }
            }
            return null;
        }

        /// <summary>
        /// Returns a region by argument
        /// </summary>
        /// <param name="uuid">A regionHandle of the region to return</param>
        /// <returns>A SimProfileData for the region</returns>
        public SimProfileData getRegion(ulong handle)
        {
            foreach (KeyValuePair<string, IGridData> kvp in _plugins)
            {
                try
                {
                    return kvp.Value.GetProfileByHandle(handle);
                }
                catch (Exception e)
                {
                    OpenSim.Framework.Console.MainConsole.Instance.WriteLine("getRegionPlugin Handle " + kvp.Key + " is made of fail: " + e.ToString());
                }
            }
            return null;
        }

        /// <summary>
        /// Returns a XML String containing a list of the neighbouring regions
        /// </summary>
        /// <param name="reqhandle">The regionhandle for the center sim</param>
        /// <returns>An XML string containing neighbour entities</returns>
        public string GetXMLNeighbours(ulong reqhandle)
        {
            string response = "";
            SimProfileData central_region = getRegion(reqhandle);
            SimProfileData neighbour;
            for (int x = -1; x < 2; x++) for (int y = -1; y < 2; y++)
                {
                    if (getRegion(Util.UIntsToLong((uint)((central_region.regionLocX + x) * 256), (uint)(central_region.regionLocY + y) * 256)) != null)
                    {
                        neighbour = getRegion(Util.UIntsToLong((uint)((central_region.regionLocX + x) * 256), (uint)(central_region.regionLocY + y) * 256));
                        response += "<neighbour>";
                        response += "<sim_ip>" + neighbour.serverIP + "</sim_ip>";
                        response += "<sim_port>" + neighbour.serverPort.ToString() + "</sim_port>";
                        response += "<locx>" + neighbour.regionLocX.ToString() + "</locx>";
                        response += "<locy>" + neighbour.regionLocY.ToString() + "</locy>";
                        response += "<regionhandle>" + neighbour.regionHandle.ToString() + "</regionhandle>";
                        response += "</neighbour>";

                    }
                }
            return response;
        }

        /// <summary>
        /// Performed when a region connects to the grid server initially.
        /// </summary>
        /// <param name="request">The XMLRPC Request</param>
        /// <returns>Startup parameters</returns>
        public XmlRpcResponse XmlRpcLoginToSimulatorMethod(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            response.Value = responseData;

            SimProfileData TheSim = null;
            Hashtable requestData = (Hashtable)request.Params[0];

            if (requestData.ContainsKey("UUID"))
            {
                TheSim = getRegion(new LLUUID((string)requestData["UUID"]));
            }
            else if (requestData.ContainsKey("region_handle"))
            {
                TheSim = getRegion((ulong)Convert.ToUInt64(requestData["region_handle"]));
            }

            if (TheSim == null)
            {
                responseData["error"] = "sim not found";
            }
            else
            {

                ArrayList SimNeighboursData = new ArrayList();

                SimProfileData neighbour;
                Hashtable NeighbourBlock;
                for (int x = -1; x < 2; x++) for (int y = -1; y < 2; y++)
                    {
                        if (getRegion(Helpers.UIntsToLong((uint)((TheSim.regionLocX + x) * 256), (uint)(TheSim.regionLocY + y) * 256)) != null)
                        {
                            neighbour = getRegion(Helpers.UIntsToLong((uint)((TheSim.regionLocX + x) * 256), (uint)(TheSim.regionLocY + y) * 256));

                            NeighbourBlock = new Hashtable();
                            NeighbourBlock["sim_ip"] = neighbour.serverIP;
                            NeighbourBlock["sim_port"] = neighbour.serverPort.ToString();
                            NeighbourBlock["region_locx"] = neighbour.regionLocX.ToString();
                            NeighbourBlock["region_locy"] = neighbour.regionLocY.ToString();
                            NeighbourBlock["UUID"] = neighbour.UUID.ToString();

                            if (neighbour.UUID != TheSim.UUID) SimNeighboursData.Add(NeighbourBlock);
                        }
                    }

                responseData["UUID"] = TheSim.UUID.ToString();
                responseData["region_locx"] = TheSim.regionLocX.ToString();
                responseData["region_locy"] = TheSim.regionLocY.ToString();
                responseData["regionname"] = TheSim.regionName;
                responseData["estate_id"] = "1";
                responseData["neighbours"] = SimNeighboursData;

                responseData["sim_ip"] = TheSim.serverIP;
                responseData["sim_port"] = TheSim.serverPort.ToString();
                responseData["asset_url"] = TheSim.regionAssetURI;
                responseData["asset_sendkey"] = TheSim.regionAssetSendKey;
                responseData["asset_recvkey"] = TheSim.regionAssetRecvKey;
                responseData["user_url"] = TheSim.regionUserURI;
                responseData["user_sendkey"] = TheSim.regionUserSendKey;
                responseData["user_recvkey"] = TheSim.regionUserRecvKey;
                responseData["authkey"] = TheSim.regionSecret;

                // New! If set, use as URL to local sim storage (ie http://remotehost/region.yap)
                responseData["data_uri"] = TheSim.regionDataURI;
            }

            return response;
        }

        /// <summary>
        /// Performs a REST Get Operation
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public string RestGetRegionMethod(string request, string path, string param)
        {
            return RestGetSimMethod("", "/sims/", param);
        }

        /// <summary>
        /// Performs a REST Set Operation
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public string RestSetRegionMethod(string request, string path, string param)
        {
            return RestSetSimMethod("", "/sims/", param);
        }

        /// <summary>
        /// Returns information about a sim via a REST Request
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <returns>Information about the sim in XML</returns>
        public string RestGetSimMethod(string request, string path, string param)
        {
            string respstring = String.Empty;

            SimProfileData TheSim;
            LLUUID UUID = new LLUUID(param);
            TheSim = getRegion(UUID);

            if (!(TheSim == null))
            {
                respstring = "<Root>";
                respstring += "<authkey>" + TheSim.regionSendKey + "</authkey>";
                respstring += "<sim>";
                respstring += "<uuid>" + TheSim.UUID.ToString() + "</uuid>";
                respstring += "<regionname>" + TheSim.regionName + "</regionname>";
                respstring += "<sim_ip>" + TheSim.serverIP + "</sim_ip>";
                respstring += "<sim_port>" + TheSim.serverPort.ToString() + "</sim_port>";
                respstring += "<region_locx>" + TheSim.regionLocX.ToString() + "</region_locx>";
                respstring += "<region_locy>" + TheSim.regionLocY.ToString() + "</region_locy>";
                respstring += "<estate_id>1</estate_id>";
                respstring += "</sim>";
                respstring += "</Root>";
            }

            return respstring;
        }

        /// <summary>
        /// Creates or updates a sim via a REST Method Request
        /// BROKEN with SQL Update
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <returns>"OK" or an error</returns>
        public string RestSetSimMethod(string request, string path, string param)
        {
            Console.WriteLine("SimProfiles.cs:RestSetSimMethod() - processing request......");
            SimProfileData TheSim;
            TheSim = getRegion(new LLUUID(param));
            if ((TheSim) == null)
            {
                TheSim = new SimProfileData();
                LLUUID UUID = new LLUUID(param);
                TheSim.UUID = UUID;
            }

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(request);
            XmlNode rootnode = doc.FirstChild;
            XmlNode authkeynode = rootnode.ChildNodes[0];
            if (authkeynode.Name != "authkey")
            {
                return "ERROR! bad XML - expected authkey tag";
            }

            XmlNode simnode = rootnode.ChildNodes[1];
            if (simnode.Name != "sim")
            {
                return "ERROR! bad XML - expected sim tag";
            }

            if (authkeynode.InnerText != TheSim.regionRecvKey)
            {
                return "ERROR! invalid key";
            }
            for (int i = 0; i < simnode.ChildNodes.Count; i++)
            {
                switch (simnode.ChildNodes[i].Name)
                {
                    case "regionname":
                        TheSim.regionName = simnode.ChildNodes[i].InnerText;
                        break;

                    case "sim_ip":
                        TheSim.serverIP = simnode.ChildNodes[i].InnerText;
                        break;

                    case "sim_port":
                        TheSim.serverPort = Convert.ToUInt32(simnode.ChildNodes[i].InnerText);
                        break;

                    case "region_locx":
                        TheSim.regionLocX = Convert.ToUInt32((string)simnode.ChildNodes[i].InnerText);
                        TheSim.regionHandle = Helpers.UIntsToLong((TheSim.regionLocX * 256), (TheSim.regionLocY * 256));
                        break;

                    case "region_locy":
                        TheSim.regionLocY = Convert.ToUInt32((string)simnode.ChildNodes[i].InnerText);
                        TheSim.regionHandle = Helpers.UIntsToLong((TheSim.regionLocX * 256), (TheSim.regionLocY * 256));
                        break;
                }
            }

            try
            {
                // NEEDS IMPLEMENTATION.
                return "OK";
            }
            catch (Exception e)
            {
                return "ERROR! could not save to database! (" + e.ToString() + ")";
            }

        }

    }
}
