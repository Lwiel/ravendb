import IndexLockMode = Raven.Client.Documents.Indexes.IndexLockMode;
import IndexPriority = Raven.Client.Documents.Indexes.IndexPriority;
import saveIndexPriorityCommand from "commands/database/index/saveIndexPriorityCommand";
import database from "models/resources/database";
import saveIndexLockModeCommand from "commands/database/index/saveIndexLockModeCommand";
import { IndexSharedInfo } from "../models/indexes";
import getIndexesStatsCommand from "commands/database/index/getIndexesStatsCommand";
import IndexUtils from "../utils/IndexUtils";


export default class IndexesService {
    
    async setLockMode(indexes: IndexSharedInfo[], lockMode: IndexLockMode, db: database) {
        await new saveIndexLockModeCommand(indexes, lockMode, db, IndexUtils.formatLockMode(lockMode))
            .execute();
    }
    
    async setPriority(index: IndexSharedInfo, priority: IndexPriority, db: database) {
        await new saveIndexPriorityCommand(index.name, priority, db)
            .execute();
    }
    
    async getStats(db: database, location: databaseLocationSpecifier) {
        const stats = await new getIndexesStatsCommand(db, location)
            .execute();
        
        return stats;
    }
}
