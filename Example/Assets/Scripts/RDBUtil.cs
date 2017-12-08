using System;
using System.Linq;
using System.Collections.Generic;
using SQLite4Unity3d;

public class RDBUtil{
	/// <summary>
	/// <para>便宜上、PrimaryKeyとなるカラムの一覧の組み合わせを返す</para>
	/// <para>※ 【注意】サーバー側の都合に合わせてあげるための実装</para>
	/// <para>※ SQLiteのライブラリの関係上、複合PrimaryKeyは持てない。PrimaryKeyであるがAutoIncreamentであるものはPrimaryKeyとせず、UniqueIndexの組み合わせのものを仮想的なPrimaryKeyとしてあげている。</para>
	/// <para>※ 【第一引数】情報を取得するテーブルのType</para>
	/// </summary>
	public static List<List<TableMapping.Column>> GetVirtualPrimaryKeyColumnCombination(Type tableType){
           List<TableMapping.Column> columns = CustomDataService.Instance.LoadColumns(tableType);
		List<List<TableMapping.Column>> pkColumnList = new List<List<TableMapping.Column>>();
		List<TableMapping.Column> pkCokumns = columns.FindAll(c => c.IsPK && !c.IsAutoInc);
		pkColumnList.Add(pkCokumns);
		if(pkCokumns.Count <= 0){
			Dictionary<string, List<TableMapping.Column>> uniqueIndexNameColumns = new Dictionary<string, List<TableMapping.Column>>();
			for(int i = 0;i < columns.Count;++i){
				List<IndexedAttribute> iaList = columns[i].Indices.ToList();
				for(int j = 0;j < iaList.Count;++j){
					if(iaList[j].Unique){
						if(!uniqueIndexNameColumns.ContainsKey(iaList[j].Name)){
							uniqueIndexNameColumns.Add(iaList[j].Name, new List<TableMapping.Column>());
						}
						uniqueIndexNameColumns[iaList[j].Name].Add(columns[i]);
					}
				}
			}
			List<string> keys = uniqueIndexNameColumns.Keys.ToList();
			for(int i = 0;i < keys.Count;++i){
				pkColumnList.Add(uniqueIndexNameColumns[keys[i]]);
			}
		}
		return pkColumnList;
	}
       /*
	public static RDBTableRelation<T> LoadFromVirtualPrimaryKeys<T>(RDBTableRelation<T> currentDataList) where T : RDBTableData{
		Dictionary<string, object> sqlFilterParams = new Dictionary<string, object> ();
		List<List<TableMapping.Column>> pkColumnList = RDBUtil.GetVirtualPrimaryKeyColumnCombination(typeof(T));
		for (int i = 0; i < pkColumnList.Count; ++i) {
			for (int j = 0; j < pkColumnList [i].Count; ++j) {
				sqlFilterParams.Add (pkColumnList [i] [j].Name, new List<object> ());
			}
		}
		for (int i = 0; i < currentDataList.Count; ++i) {
			for (int j = 0; j < pkColumnList.Count; ++j) {
				List<TableMapping.Column> pkColumns = pkColumnList [j];
				for (int k = 0; k < pkColumns.Count; ++k) {
					TableMapping.Column pkColumn = pkColumns [k];
					List<object> values = (List<object>) sqlFilterParams[pkColumn.Name];
					values.Add (pkColumn.GetValue(currentDataList[i]));
				}
			}
		}
		List<T> results = DataService.Instance.FindByAll<T>(sqlFilterParams);
		return new RDBTableRelation<T> (results);
	}
	*/
}