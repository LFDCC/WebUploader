using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Web.Controllers
{
    public class UploaderController : Controller
    {
        /// <summary>
        /// 分片文件目录
        /// </summary>
        private const string ChunksDir = "/UpFile/chunks";
        /// <summary>
        /// 完整文件目录
        /// </summary>
        private const string FilesDir = "/UpFile/files";

        // GET: Uploader
        public ActionResult Index()
        {
            return View();
        }

        #region 获取指定文件的已上传的文件块
        /// <summary>
        /// 检测文件是否存在
        /// </summary>
        /// <returns></returns>
        public ActionResult CheckFullFile()
        {
            try
            {
                var md5 = Convert.ToString(Request["md5"]);
                var ext = Convert.ToString(Request["ext"]);
                var size = Convert.ToInt64(Request["size"]);

                var fileName = md5 + "." + ext;
                var filePath = Server.MapPath(FilesDir) + "/" + fileName;//当前文件目录

                FileInfo file = new FileInfo(filePath);
                if (!file.Exists)
                {
                    var chunkNums = 0;
                    var chunkPath = Server.MapPath(ChunksDir + "/" + md5);//当前分片文件目录
                    if (Directory.Exists(chunkPath))
                    {
                        DirectoryInfo dicInfo = new DirectoryInfo(chunkPath);
                        var files = dicInfo.GetFiles();
                        chunkNums = files.Count();
                        if (chunkNums > 1)
                        {
                            chunkNums = chunkNums - 1; //当文件上传中时，页面刷新，上传中断，这时最后一个保存的块的大小可能会有异常，所以这里直接删除最后一个块文件
                        }
                    }
                    return Json(new { ifExist = file.Exists, chunkNums }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { ifExist = file.Exists }, JsonRequestBehavior.AllowGet);
                }


            }
            catch (Exception e)
            {
                return Json(new { ifExist = false }, JsonRequestBehavior.AllowGet);
            }
        }
        #endregion

        #region 验证分片文件
#if false

                    /// <summary>
                    /// 验证分片文件是否存在且完整
                    /// </summary>
                    /// <returns></returns>
                    public ActionResult CheckChunkFile(string md5, int chunk, long chunkSize)
                    {
                        string chunkPath = Server.MapPath(ChunksDir + "/" + md5 + "/" + chunk);
                        FileInfo fileInfo = new FileInfo(chunkPath);

                        if (!fileInfo.Exists || fileInfo.Length < chunkSize)//文件不存在 或者 文件大小异常 都重新上传
                        {
                            return Json(new { isCheck = false }, JsonRequestBehavior.AllowGet);
                        }
                        else
                        {
                            return Json(new { isCheck = true }, JsonRequestBehavior.AllowGet);
                        }
                  }
#endif
        #endregion


        #region 上传文件
        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="fileData"></param>
        /// <returns></returns>
        public ActionResult Upload(HttpPostedFileBase file)
        {
            if (file != null)
            {
                string md5 = Request["md5"];
                //如果进行了分片
                if (Request.Form.AllKeys.Any(m => m == "chunk"))
                {
                    int chunk = Convert.ToInt32(Request.Form["chunk"]);//当前分片在上传分片中的顺序（从0开始）
                    string chunkDir = Server.MapPath(ChunksDir + "/" + md5);//当前分片文件目录
                    string chunkPath = chunkDir + "/" + chunk;//分片文件路径

                    //建立临时传输文件夹
                    if (!Directory.Exists(chunkDir))
                    {
                        Directory.CreateDirectory(chunkDir);
                    }
                    file.SaveAs(chunkPath);
                    return Json(new { isUpload = true, ext = Path.GetExtension(file.FileName) }, JsonRequestBehavior.AllowGet);
                }
                else//没有分片直接保存
                {
                    var fileDir = Server.MapPath(FilesDir) + "/" + md5;
                    string filePath = fileDir + Path.GetExtension(Request.Files[0].FileName);
                    file.SaveAs(filePath);
                    return Json(new { isUpload = true, savePath = filePath }, JsonRequestBehavior.AllowGet);
                }
            }
            else
            {
                return Json(new { isUpload = false }, JsonRequestBehavior.AllowGet);
            }
        }
        #endregion

        #region 合并文件
        /// <summary>
        /// 合并文件
        /// </summary>
        /// <returns></returns>
        public ActionResult MergeFiles()
        {
            string md5 = Request["md5"];
            string ext = Request["ext"];
            var fileName = md5 + "." + ext;
            string sourcePath = Server.MapPath(ChunksDir + "/" + md5);//源数据文件夹
            string targetFilePath = Server.MapPath(FilesDir) + "/" + fileName; //合并后的文件

            if (!Directory.Exists(Server.MapPath(FilesDir)))
            {
                Directory.CreateDirectory(Server.MapPath(FilesDir));
            }
            if (Directory.Exists(sourcePath))
            {
                DirectoryInfo dicInfo = new DirectoryInfo(sourcePath);
                FileInfo[] files = dicInfo.GetFiles();
                foreach (FileInfo file in files.OrderBy(f => int.Parse(f.Name)))
                {
                    FileStream addFile = new FileStream(targetFilePath, FileMode.Append, FileAccess.Write);
                    BinaryWriter AddWriter = new BinaryWriter(addFile);

                    //获得上传的分片数据流 
                    Stream stream = file.Open(FileMode.Open);
                    BinaryReader TempReader = new BinaryReader(stream);
                    //将上传的分片追加到临时文件末尾
                    AddWriter.Write(TempReader.ReadBytes((int)stream.Length));
                    //关闭BinaryReader文件阅读器
                    TempReader.Close();
                    stream.Close();
                    AddWriter.Close();
                    addFile.Close();

                    TempReader.Dispose();
                    stream.Dispose();
                    AddWriter.Dispose();
                    addFile.Dispose();
                }
                DeleteFolder(sourcePath);
                return Json(new { isMerge = true, savePath = HttpUtility.UrlEncode(targetFilePath) }, JsonRequestBehavior.AllowGet);
            }
            else
            {
                return Json(new { isMerge = false, savePath = HttpUtility.UrlEncode(targetFilePath) }, JsonRequestBehavior.AllowGet);
            }
        }
        #endregion

        #region 删除文件夹及其内容
        /// <summary>
        /// 删除文件夹及其内容
        /// </summary>
        /// <param name="dir"></param>
        private void DeleteFolder(string strPath)
        {
            //删除这个目录下的所有子目录
            if (Directory.GetDirectories(strPath).Length > 0)
            {
                foreach (string fl in Directory.GetDirectories(strPath))
                {
                    Directory.Delete(fl, true);
                }
            }
            //删除这个目录下的所有文件
            if (Directory.GetFiles(strPath).Length > 0)
            {
                foreach (string f in Directory.GetFiles(strPath))
                {
                    System.IO.File.Delete(f);
                }
            }
            Directory.Delete(strPath, true);
        }
        #endregion
    }
}