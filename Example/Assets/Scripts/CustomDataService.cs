using SQLite4Unity3d;
using UnityEngine;
using System;
using System.Text;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class CustomDataService{
	private SQLiteConnection connection;
	private HashSet<Type> existTableTypes = new HashSet<Type>();

    private static string saveDBFilePathCache;
    private static string streamingAssetPathCache;

    private static CustomDataService instance;

    public static CustomDataService Instance{
		get{
			if(instance == null){
                instance = new CustomDataService ();
			}
			return instance;
		}
	}

	/// <summary>
	/// <para>※SQLiteはThreadで使うこともあるので、Setupメソッドを用意して、そこでちゃんとSetupしておくようにする</para>
	/// </summary>
	public static void Setup(){
        saveDBFilePathCache = SavedDBFilePath;
        streamingAssetPathCache = StreamingAssetsDBFilePath;
		//SQLllogを出すか出さないか
		//SQLiteLogger.Enabled = PlayerPrefs.GetInt(DebuggerConst.ShowSQLLogKey, 1) == 1;
    }

    private CustomDataService(){
		string dbPath = SavedDBFilePath;

        #if !UNITY_EDITOR
		if (!File.Exists(dbPath)){
			string streamingAssetsFilePath = StreamingAssetsDBFilePath;
			#if UNITY_ANDROID
			WWW loadDb = new WWW(streamingAssetsFilePath); // this is the path to your StreamingAssets in android
			while (!loadDb.isDone){ } // CAREFUL here, for safety reasons you shouldn't let this while loop unattended, place a timer and error check
			// then save to Application.persistentDataPath
			File.WriteAllBytes(dbPath, loadDb.bytes);
			#else
			File.Copy(streamingAssetsFilePath, dbPath);
			#endif
		}
        #endif
		connection = ConnectSQLite (dbPath);
	}

	private SQLiteConnection ConnectSQLite(string dbPath){
		SQLiteConnection SQLitecon = new SQLiteConnection(dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create);
//		SQLiteLogger.LogSQLTrace (SQLitecon);
		return SQLitecon;
	}

	public void CreateTable(Type objectType){
		if (existTableTypes.Contains (objectType)) {
			return;
		}
		connection.CreateTable(objectType);
		existTableTypes.Add (objectType);
	}

	public List<SQLiteConnection.ColumnInfo> GetColumnsInfo(Type type){
		TableMapping mapping = connection.GetMapping (type);
		return connection.GetTableInfo(mapping.TableName);
	}

	public List<TableMapping.Column> LoadColumns(Type type){
		TableMapping mapping = connection.GetMapping (type);
		return new List<TableMapping.Column>(mapping.Columns);
	}

	public string GetTableName(Type type){
		TableMapping mapping = connection.GetMapping (type);
		return mapping.TableName;
	}

	public TableQuery<T> LoadTable<T>(){
		return new TableQuery<T>(connection);
	}

	// 引数なしで全件数受け取る
	public List<T> FindByAll<T>(Dictionary<string, object> filterDic = null){
		TableMapping mapping = connection.GetMapping (typeof(T));
		return connection.Query(mapping, SQLBuildHelper.GenerateSelectQueryString(mapping.TableName, filterDic)).Cast<T>().ToList();
	}

	public T FindBy<T>(Dictionary<string, object> filterDic){
		TableMapping mapping = connection.GetMapping (typeof(T));
		return (T) connection.Query(mapping, SQLBuildHelper.GenerateSelectQueryString(mapping.TableName, filterDic)).FirstOrDefault();
	}

	//2重でTransactionを実行しようとさせないために、
	private HashSet<Action> transactionStack = new HashSet<Action>();

	public void Transaction(Action inTransaction){
		if (transactionStack.Count <= 0) {
			connection.BeginTransaction ();
		}
		try{
			transactionStack.Add(inTransaction);
			inTransaction();
			transactionStack.Remove(inTransaction);
			if (transactionStack.Count <= 0) {
				connection.Commit();
			}
		} catch (Exception ex) {
			StringBuilder strBuilder = new StringBuilder();
			strBuilder.Append ("<color=#ff0000ff>");
			strBuilder.Append (string.Format("SQL Exception!!:{0}", ex.Message));
			strBuilder.Append ("</color>");
			strBuilder.Append("\n\n");
			strBuilder.Append ("<color=#ff0000ff>");
			strBuilder.Append (ex.StackTrace);
			strBuilder.Append ("</color>");
			strBuilder.Append("\n\n");
			//SQLiteLogger.Log (strBuilder.ToString());
			connection.Rollback();
			transactionStack.Clear ();
			// ErrorとしてEditorに出力させる。でも処理は止まってくれないので、そのあとにthrowする
			Debug.LogError (ex);
			throw ex;
		}
	}

	public int Insert(object obj){
        return connection.Execute(SQLBuildHelper.GenerateInsertQueryFromObject(obj.GetType(), obj));
	}

	public int InsertOrReplace(object obj)
	{
		return connection.Execute(SQLBuildHelper.GenerateInsertOrReplaceQueryFromObject(obj.GetType(), obj));
	}

	public int InsertAll(Type obejctType, IEnumerable objects){
        string sql = SQLBuildHelper.GenerateInsertQueryFromObject(obejctType, objects);
        if(string.IsNullOrEmpty(sql)){
            return 0;
        }
	    return connection.Execute(sql);
	}

	public int InsertOrReplaceAll(Type obejctType, IEnumerable objects){
		string sql = SQLBuildHelper.GenerateInsertQueryFromObject(obejctType, objects);
		if (string.IsNullOrEmpty(sql))
		{
			return 0;
		}
        return connection.Execute(sql);
	}

	public int Delete(object obj){
		return connection.Delete(obj);
	}

	public int Update(object obj){
		return connection.Execute(SQLBuildHelper.GenerateUpdateQueryFromObject(obj));
		//return connection.Update(obj);
	}

	public int UpdateAll(IEnumerable objects){
		int count = 0;
		IEnumerator enumerator = objects.GetEnumerator ();
		while (enumerator.MoveNext()){
			count += connection.Execute(SQLBuildHelper.GenerateUpdateQueryFromObject(enumerator.Current));
		}
		return count;
		//return connection.UpdateAll(objects);
	}

	public static string ApplicationDatabaseName{
		get{
			List<string> list = new List<string> ();

			list.Add ("huracan.db");
			return string.Join("_", list.ToArray());
		}
	}

	public static string StreamingAssetsDatabaseName{
		get{
			List<string> list = new List<string> ();

			list.Add ("huracan.db");

			return string.Join("_", list.ToArray());
		}
	}
		
	public static string StreamingAssetsDBFilePath{
		get{
			if (string.IsNullOrEmpty(streamingAssetPathCache)){
    			string streamingAssetPath = Application.streamingAssetsPath;
                #if !UNITY_ANDROID
	    		if (!Directory.Exists(streamingAssetPath)){
		    		Directory.CreateDirectory(streamingAssetPath);
			    }
                #endif

                streamingAssetPathCache = Path.Combine(streamingAssetPath, CustomDataService.StreamingAssetsDatabaseName);
			}

			return streamingAssetPathCache;
		}
	}

	public static string SavedDBFilePath{
		get{
			if (string.IsNullOrEmpty (saveDBFilePathCache)) {
				saveDBFilePathCache = Path.Combine (Application.persistentDataPath, ApplicationDatabaseName);
			}
			return saveDBFilePathCache;
		}
	}

	public static void ClearDB(){
		// すでに開いているconnectionがあればconnectionを閉じる
        CustomDataService.Instance.CloseDB ();
        string dbPath = CustomDataService.SavedDBFilePath;
		if (File.Exists (dbPath)) {
			File.Delete (dbPath);
		}
		Debug.Log ("Cleared DBFile:" + dbPath);
		// 一時的にConnectionをもう一回開くことでDBファイルを作る。そしてCloseしてもらう。
        CustomDataService.Instance.CloseDB ();
	}

	public void CloseDB(){
		if (connection != null) {
			connection.Close ();
		}
		connection = null;
		// Closeしたら自動でConnectionしてほしいのでinstanceもnullにして一連の処理をやり直してもらう
		instance = null;
	}
}